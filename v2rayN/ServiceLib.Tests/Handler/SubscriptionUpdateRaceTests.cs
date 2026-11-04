using AwesomeAssertions;
using ServiceLib.Tests.CoreConfig;
using TUnit.Core;

namespace ServiceLib.Tests.Handler;

[NotInParallel]
public class SubscriptionUpdateRaceTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DelayedResponseCannotUndoSavedUserEdit(bool changeUrl)
    {
        // Keep this integration test confined to the test executable's database.
        Path.GetFullPath(Utils.StartupPath()).Should().Be(Path.GetFullPath(Utils.GetBaseDirectory()));
        var config = CoreConfigTestFactory.CreateConfig();
        await CertPemManager.Instance.Init(config);
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var id = $"race-{Guid.NewGuid():N}";
        var originalUrl = $"http://127.0.0.1:{port}/original";
        var editedUrl = $"http://127.0.0.1:{port}/edited";
        var sub = new SubItem { Id = id, Url = originalUrl, Remarks = "test" };
        var profile = new ProfileItem { IndexId = Guid.NewGuid().ToString(), Subid = id, IsSub = true };
        try
        {
            await ConfigHandler.AddSubItem(config, sub);
            await SQLiteHelper.Instance.InsertAsync(profile);
            var tickets = await SubscriptionWrites.Capture([id]);
            var download = SubscriptionHandler.UpdateProcess(config, id, false, (_, _) => Task.CompletedTask, tickets);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = peer.GetStream();
            using var request = new StreamReader(stream, leaveOpen: true);
            while (await request.ReadLineAsync(timeout.Token) is { Length: > 0 }) { }

            if (changeUrl)
            {
                sub.Url = editedUrl;
                await ConfigHandler.AddSubItem(config, sub);
            }
            else
            {
                await ConfigHandler.RemoveServers(config, [profile]);
            }

            var body = "socks://127.0.0.1:1080#stale";
            var response = $"HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), timeout.Token);
            (await download.WaitAsync(timeout.Token)).Should().BeFalse();
            (await tickets[id].Restore(() => throw new InvalidOperationException("Stale snapshot"))).Should().BeFalse();
            (await AppManager.Instance.GetSubItem(id))!.Url.Should().Be(changeUrl ? editedUrl : originalUrl);
            var nodes = await AppManager.Instance.ProfileItems(id) ?? [];
            nodes.Select(p => p.IndexId).Should().Equal(changeUrl ? [profile.IndexId] : Array.Empty<string>());
        }
        finally
        {
            await ConfigHandler.DeleteSubItem(config, id);
        }
    }

    [Test]
    public async Task NextFailedUpdateCannotRestoreNodesDeletedBeforeDownload()
    {
        Path.GetFullPath(Utils.StartupPath()).Should().Be(Path.GetFullPath(Utils.GetBaseDirectory()));
        var config = CoreConfigTestFactory.CreateConfig();
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        SQLiteHelper.Instance.CreateTable<ProfileExItem>();
        var id = $"snapshot-{Guid.NewGuid():N}";
        var profile = new ProfileItem { IndexId = Guid.NewGuid().ToString(), Subid = id, IsSub = true };
        config.IndexId = profile.IndexId;
        try
        {
            await ConfigHandler.AddSubItem(config, new SubItem { Id = id, Url = "http://127.0.0.1", RequestHeaders = "invalid" });
            await SQLiteHelper.Instance.InsertAsync(profile);
            (await SubscriptionSnapshotHandler.CaptureAsync(config, id)).Should().BeTrue();
            await ConfigHandler.RemoveServers(config, [profile]);
            SubscriptionSnapshotHandler.TryGetSnapshotTimestamp(id).Should().BeNull();
            var tickets = await SubscriptionWrites.Capture([id]);
            (await tickets[id].IfCurrent(() => SubscriptionSnapshotHandler.CaptureAsync(config, id))).Should().BeFalse();
            (await SubscriptionHandler.UpdateProcess(config, id, false, (_, _) => Task.CompletedTask, tickets)).Should().BeFalse();
            (await tickets[id].Restore(() => SubscriptionSnapshotHandler.RestoreAsync(config, id))).Should().BeFalse();
            (await AppManager.Instance.ProfileItems(id))!.Should().BeEmpty();
        }
        finally
        {
            await ConfigHandler.DeleteSubItem(config, id);
        }
    }

    [Test]
    public async Task SubscriptionSelectionUsesOnlyFreshPositiveMeasurementsOfExistingNodes()
    {
        Path.GetFullPath(Utils.StartupPath()).Should().Be(Path.GetFullPath(Utils.GetBaseDirectory()));
        var config = CoreConfigTestFactory.CreateConfig();
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        SQLiteHelper.Instance.CreateTable<ProfileExItem>();
        var good = Guid.NewGuid().ToString();
        var failed = Guid.NewGuid().ToString();
        var node = new ProfileItem { IndexId = Guid.NewGuid().ToString(), Subid = good, IsSub = true };
        var old = new ProfileItem { IndexId = Guid.NewGuid().ToString(), Subid = failed, IsSub = true };
        var historical = new ProfileExItem { IndexId = old.IndexId, Delay = 1 };
        try
        {
            foreach (var id in new[] { good, failed })
            {
                await ConfigHandler.AddSubItem(config, new SubItem { Id = id, Url = "http://127.0.0.1" });
            }
            await SQLiteHelper.Instance.InsertAllAsync(new[] { node, old });
            await SQLiteHelper.Instance.InsertAsync(historical);
            var delays = new Dictionary<string, int> { [node.IndexId] = 86, ["deleted-node"] = 2 };
            var result = await ServiceLib.ViewModels.ProfilesViewModel.ReadSubscriptionLatencyResult([good, failed], 3, delays);
            result.AvailableSubscriptionIds.Should().Equal(good);
            result.BestProfile!.IndexId.Should().Be(node.IndexId);
            result.BestProfile.Delay.Should().Be(86);
        }
        finally
        {
            await SQLiteHelper.Instance.DeleteAsync(historical);
            await ConfigHandler.DeleteSubItem(config, good);
            await ConfigHandler.DeleteSubItem(config, failed);
        }
    }

    [Test]
    public async Task FailedImportCanStillRestoreThePreUpdateSnapshot()
    {
        Path.GetFullPath(Utils.StartupPath()).Should().Be(Path.GetFullPath(Utils.GetBaseDirectory()));
        var config = CoreConfigTestFactory.CreateConfig();
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();
        SQLiteHelper.Instance.CreateTable<ProfileExItem>();
        var id = Guid.NewGuid().ToString();
        var node = new ProfileItem { IndexId = Guid.NewGuid().ToString(), Subid = id, IsSub = true };
        config.IndexId = node.IndexId;
        try
        {
            await ConfigHandler.AddSubItem(config, new SubItem { Id = id, Url = "http://127.0.0.1" });
            await SQLiteHelper.Instance.InsertAsync(node);
            (await SubscriptionSnapshotHandler.CaptureAsync(config, id)).Should().BeTrue();
            var ticket = (await SubscriptionWrites.Capture([id]))[id];
            await ticket.Apply(async () =>
            {
                await ConfigHandler.RemoveServers(config, [node]);
                return false;
            });
            (await ticket.Restore(() => SubscriptionSnapshotHandler.RestoreAsync(config, id))).Should().BeTrue();
            (await AppManager.Instance.ProfileItems(id))!.Should().ContainSingle(item => item.IndexId == node.IndexId);
        }
        finally
        {
            await ConfigHandler.DeleteSubItem(config, id);
        }
    }
}
