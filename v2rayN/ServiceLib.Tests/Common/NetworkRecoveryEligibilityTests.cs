using AwesomeAssertions;
using TUnit.Core;

namespace ServiceLib.Tests.Common;

public class NetworkRecoveryEligibilityTests
{
    [Test]
    [Arguments(false, false, false, false, "暂无订阅")]
    [Arguments(true, false, false, true, "订阅暂无节点")]
    [Arguments(false, true, false, true, "已配置本地节点")]
    [Arguments(true, true, true, true, "尚未检测网络")]
    public void ConfigurationDeterminesIdleStatusAndAllowedActions(
        bool hasSubscription, bool hasProfiles, bool canMonitor, bool canSetUp, string message)
    {
        var configuration = new NetworkRecoveryEligibility(hasSubscription, hasProfiles);
        configuration.CanMonitor.Should().Be(canMonitor);
        configuration.CanSetUp.Should().Be(canSetUp);
        configuration.IdleMessage.Should().Be(message);
    }

    [Test]
    [Arguments(true, true, true, true)]
    [Arguments(true, true, false, true)]
    [Arguments(false, true, true, true)]
    [Arguments(false, true, false, false)]
    [Arguments(false, false, true, false)]
    public void RefreshDuringSetupPreservesRuntimeStatus(
        bool hasSubscription, bool hasProfiles, bool running, bool expected)
    {
        var before = new NetworkRecoveryEligibility(true, false);
        var after = new NetworkRecoveryEligibility(hasSubscription, hasProfiles);
        after.ShouldKeepRuntimeStatus(before, running).Should().Be(expected);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task LocalSetupVerifiesCandidatesAndRetriesOriginalIfAllFail(bool secondWorks)
    {
        var measured = new Dictionary<string, int>
        {
            ["second"] = 80, ["first"] = 20, ["unreachable"] = -1, ["untested"] = 0
        };
        var attempts = new List<string>();
        var verified = new List<string>();
        var success = await ServiceLib.ViewModels.MainWindowViewModel.TryLocalNodes(
            LatencySortHelper.ReachableNodeIds(measured), "original",
            id =>
            {
                attempts.Add(id);
                if (id == "first")
                {
                    throw new IOException("Candidate could not start");
                }
                verified.Add(id);
                return Task.FromResult(secondWorks && id == "second");
            });

        success.Should().Be(secondWorks);
        attempts.Should().Equal(secondWorks ? ["first", "second"] : ["first", "second", "original"]);
        verified.Should().Equal(secondWorks ? ["second"] : ["second", "original"]);
    }

    [Test]
    [Arguments(false, 1, 2, false)]
    [Arguments(false, 2, 2, false)]
    [Arguments(true, 1, 2, false)]
    [Arguments(true, 2, 2, true)]
    public void LateMonitorResultRequiresSameRevisionAndEligibleConfiguration(
        bool hasProfiles, int observedRevision, int currentRevision, bool expected)
    {
        var configuration = new NetworkRecoveryEligibility(true, hasProfiles);
        var monitor = new NetworkAvailabilityInfo(ENetworkAvailabilityState.Repairing, "", MonitorRevision: observedRevision);
        NetworkRecoveryEligibility.AcceptsStatus(configuration, monitor, currentRevision).Should().Be(expected);
        NetworkRecoveryEligibility.AcceptsStatus(configuration, monitor with { MonitorRevision = null }, currentRevision)
            .Should().BeTrue();
    }

    [Test]
    public async Task ManualSetupIsSkippedUntilSubscriptionWorkFinishes()
    {
        var running = new List<bool>();
        var gate = new NetworkWorkflowGate(running.Add);
        var started = new TaskCompletionSource<ServiceLib.ViewModels.MainWindowViewModel.RunningCore?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applying = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var configured = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var update = gate.RunAsync(() => ServiceLib.ViewModels.MainWindowViewModel.ActivateServer(
            "first", _ => started.Task, _ =>
            {
                applying.SetResult();
                return configured.Task;
            }), wait: true);
        var setups = 0;
        Task Setup()
        {
            setups++;
            return Task.CompletedTask;
        }

        await gate.RunAsync(Setup, wait: false);
        setups.Should().Be(0);
        gate.IsBusy.Should().BeTrue();
        started.SetResult(new("first"));
        await applying.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await gate.RunAsync(Setup, wait: false);
        setups.Should().Be(0);
        update.IsCompleted.Should().BeFalse();
        configured.SetResult(true);
        await update;
        await gate.RunAsync(Setup, wait: false);
        setups.Should().Be(1);
        running.Should().Equal(true, false, true, false);
    }

    [Test]
    [Arguments("old", "new", false, 0)]
    [Arguments("new", "old", false, 1)]
    [Arguments("new", "new", true, 1)]
    public async Task VerificationMustBelongToRequestedRunningProfile(
        string before, string after, bool expected, int expectedProbes)
    {
        var expectedRun = new ServiceLib.ViewModels.MainWindowViewModel.RunningCore("same-profile");
        var replacement = new ServiceLib.ViewModels.MainWindowViewModel.RunningCore("same-profile");
        var running = before == "new" ? expectedRun : replacement;
        var probes = 0;
        var connected = await ServiceLib.ViewModels.MainWindowViewModel.VerifyRunningProfile(
            expectedRun, candidate => ReferenceEquals(running, candidate), () =>
            {
                probes++;
                running = after == "new" ? expectedRun : replacement;
                return Task.FromResult(true);
            });
        connected.Should().Be(expected);
        probes.Should().Be(expectedProbes);
    }

    [Test]
    [Arguments(5, 0, false)]
    [Arguments(5, 2, false)]
    [Arguments(5, 255, true)]
    [Arguments(72, 84, true)]
    public async Task CleanupRequiresASocks5Greeting(int version, int method, bool shouldClear)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var clears = 0;
        Task<bool> Clear() { clears++; return Task.FromResult(true); }
        var cleanup = ServiceLib.ViewModels.MainWindowViewModel.ClearProxyIfSocksUnavailable(port, true, Clear);
        using var peer = await listener.AcceptTcpClientAsync();
        using var stream = peer.GetStream();
        var greeting = new byte[4];
        await stream.ReadExactlyAsync(greeting);
        greeting.Should().Equal(5, 2, 0, 2);
        await stream.WriteAsync(new byte[] { (byte)version, (byte)method });
        (await cleanup).Should().BeTrue();
        clears.Should().Be(shouldClear ? 1 : 0);

        await ServiceLib.ViewModels.MainWindowViewModel.ClearProxyIfSocksUnavailable(port, false, Clear);
        clears.Should().Be(shouldClear ? 2 : 1);
    }

    [Test]
    public void AutomaticRecoveryRejectsDeletedNodesAndChangedSubscriptionLinks()
    {
        var initial = new AutomaticRecoveryInputs([("sub", "url", "")], ["a", "b"]);
        initial.Matches(new([("sub", "url", "")], ["b", "a"])).Should().BeTrue();
        initial.Matches(new([("sub", "url", "")], [])).Should().BeFalse();
        initial.Matches(new([("sub", "url2", "")], ["a", "b"])).Should().BeFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task UserEditDuringDownloadWinsOverResponseAndRollback(bool downloadSucceeded)
    {
        var id = Guid.NewGuid().ToString();
        var ticket = (await SubscriptionWrites.Capture([id]))[id];
        var url = "A";
        var nodes = new List<string> { "original" };
        var response = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task FinishUpdate()
        {
            if (await response.Task)
            {
                await ticket.Apply(() =>
                {
                    url = "A";
                    nodes.Add("stale response");
                    return Task.FromResult(true);
                });
            }
            await ticket.Restore(() =>
            {
                nodes.Add("old snapshot");
                return Task.FromResult(true);
            });
        }
        var download = FinishUpdate();
        await SubscriptionWrites.Edit([id], () =>
        {
            url = "B";
            nodes.Clear();
            return Task.FromResult(0);
        });
        response.SetResult(downloadSucceeded);
        await download;
        url.Should().Be("B");
        nodes.Should().BeEmpty();
    }

    [Test]
    public async Task RollbackAcceptsOwnImportAndRejectsSubsequentEdit()
    {
        var id = Guid.NewGuid().ToString();
        var ticket = (await SubscriptionWrites.Capture([id]))[id];
        await ticket.Apply(async () => await SubscriptionWrites.Edit([id], () => Task.FromResult(1)) == 1);
        (await ticket.Restore(() => Task.FromResult(true))).Should().BeTrue();

        ticket = (await SubscriptionWrites.Capture([id]))[id];
        await ticket.Apply(() => Task.FromResult(true));
        await SubscriptionWrites.Edit([id], () => Task.FromResult(1));
        (await ticket.Restore(() => throw new InvalidOperationException("Stale restore"))).Should().BeFalse();
    }

    [Test]
    [Arguments(true, true, false, false, true, 0)]
    [Arguments(false, true, false, false, true, 1)]
    [Arguments(false, false, true, false, false, 1)]
    [Arguments(false, true, true, true, false, 1)]
    public void WindowsProxyReportsNativeOrFallbackOutcome(bool nativeOk, bool fallbackOk, bool nativeThrows,
        bool fallbackThrows, bool expected, int expectedFallbacks)
    {
        if (!OperatingSystem.IsWindows()) return;
        var calls = 0;
        var result = ProxySettingWindows.ApplyWithFallback(
            () => nativeThrows ? throw new IOException() : nativeOk,
            () =>
            {
                calls++;
                return fallbackThrows ? throw new UnauthorizedAccessException() : fallbackOk;
            });
        result.Should().Be(expected);
        calls.Should().Be(expectedFallbacks);
    }

    [Test]
    public void WindowsFallbackReportsDeniedRegistryWrites()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = $@"Software\v2rayN-Test-{Guid.NewGuid():N}";
        using var root = Microsoft.Win32.Registry.CurrentUser;
        try
        {
            using (var writable = root.CreateSubKey(path))
            {
                ProxySettingWindows.WriteRegistrySettings(writable, "127.0.0.1:1080", "", 2);
                writable.GetValue("ProxyEnable").Should().Be(1);
                writable.GetValue("ProxyServer").Should().Be("127.0.0.1:1080");
                ProxySettingWindows.WriteRegistrySettings(writable, null, null, 1);
                writable.GetValue("ProxyEnable").Should().Be(0);
                writable.GetValue("ProxyServer").Should().BeNull();
            }
            using var readOnly = root.OpenSubKey(path)!;
            bool WriteToReadOnlyKey()
            {
                if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
                ProxySettingWindows.WriteRegistrySettings(readOnly, "127.0.0.1:1080", "", 2);
                return true;
            }
            ProxySettingWindows.ApplyWithFallback(() => false, WriteToReadOnlyKey).Should().BeFalse();
        }
        finally
        {
            root.DeleteSubKey(path, throwOnMissingSubKey: false);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void LanModeRemainsAppliedWhenRasFails(bool enabling)
    {
        if (!OperatingSystem.IsWindows()) return;
        var previous = enabling ? ESysProxyType.ForcedClear : ESysProxyType.ForcedChange;
        var requested = enabling ? ESysProxyType.ForcedChange : ESysProxyType.ForcedClear;
        var osLan = previous;
        var attempted = new List<string>();
        var result = ProxySettingWindows.ApplyConnections(
            () => { osLan = requested; return true; },
            () => ["bad", "good"],
            name =>
            {
                attempted.Add(name);
                if (name == "bad") throw new IOException("RAS connection unavailable");
                return true;
            });
        result.Should().Be(ProxyApplyResult.LanAppliedRasIncomplete);
        attempted.Should().Equal("bad", "good");
        ServiceLib.ViewModels.StatusBarViewModel.AppliedLanMode(previous, requested, result).Should().Be(osLan);
    }
}
