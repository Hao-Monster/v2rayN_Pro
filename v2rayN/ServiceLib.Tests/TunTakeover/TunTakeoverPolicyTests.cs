using ServiceLib.Tests.CoreConfig;
using TUnit.Assertions.Should;
using TUnit.Assertions.Should.Extensions;
using TUnit.Core;

namespace ServiceLib.Tests.TunTakeover;

[NotInParallel]
public class TunTakeoverPolicyTests
{
    [Test]
    public async Task SingboxTun_DefaultConfig_GeneratesDualStackAddress_MixedStack_Mtu1500()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();

        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var rawJson = result.Data!.ToString()!;
        using var rawDoc = JsonDocument.Parse(rawJson);
        var tunJson = rawDoc.RootElement.GetProperty("inbounds").EnumerateArray()
            .First(i => i.GetProperty("type").GetString() == "tun");
        await tunJson.TryGetProperty("sniff", out _).Should().BeFalse();

        var cfg = JsonUtils.Deserialize<SingboxConfig>(rawJson)!;
        var tun = cfg.inbounds.First(i => i.type == "tun");

        await tun.stack.Should().BeEqualTo("mixed");
        await tun.mtu.Should().BeEqualTo(1500);
        await tun.address.Should().NotBeNull();
        await tun.address!.Should().Contain("172.18.0.1/30");
        await tun.address!.Should().Contain("fdfe:dcba:9876::1/126");
        await cfg.route.rules.Should().Contain(r => r.action == "sniff");
    }

    [Test]
    public async Task SingboxTun_AutoFakeIp_GeneratesDualStackFakeIpRanges_WithoutSubnetOverlap()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.SimpleDNSItem.FakeIP = false;
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.AutoFakeIPInTun = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var tunContext = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var tunResult = new CoreConfigSingboxService(tunContext).GenerateClientConfigContent();
        await tunResult.Success.Should().BeTrue().Because($"ret msg: {tunResult.Msg}");
        var tunCfg = JsonUtils.Deserialize<SingboxConfig>(tunResult.Data!.ToString())!;

        var tunInbound = tunCfg.inbounds.First(i => i.type == "tun");
        var tunIpv6 = tunInbound.address!.First(a => a.Contains(':'));
        var fakeServer = tunCfg.dns!.servers.FirstOrDefault(s => s.tag == Global.SingboxFakeDNSTag);

        await fakeServer.Should().NotBeNull();
        await fakeServer!.inet4_range.Should().BeEqualTo("198.18.0.0/15");
        await fakeServer.inet6_range.Should().BeEqualTo("fc00::/18");
        await IPNetwork2.Parse(tunIpv6).Overlap(IPNetwork2.Parse(fakeServer.inet6_range!)).Should().BeFalse();
        await tunCfg.experimental!.cache_file!.store_fakeip.Should().BeEqualTo(true);

        var fakeRule = tunCfg.dns.rules.FirstOrDefault(r => r.server == Global.SingboxFakeDNSTag);
        await fakeRule.Should().NotBeNull();
        await fakeRule!.rules.Should().NotBeNull();
        await fakeRule.rules![0].query_type.Should().Contain(1);
        await fakeRule.rules[0].query_type.Should().Contain(28);

        // Non-TUN mode must continue respecting SimpleDNSItem.FakeIP == false
        config.TunModeItem.EnableTun = false;
        var nonTunContext = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = false,
        };
        var nonTunResult = new CoreConfigSingboxService(nonTunContext).GenerateClientConfigContent();
        await nonTunResult.Success.Should().BeTrue();
        var nonTunCfg = JsonUtils.Deserialize<SingboxConfig>(nonTunResult.Data!.ToString())!;
        await nonTunCfg.dns!.servers.Any(s => s.tag == Global.SingboxFakeDNSTag).Should().BeFalse();
    }

    [Test]
    public async Task SingboxTun_OverlappingCustomTunIpv6_SelectsFallbackFakeIpv6Pool()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.EnableIPv6Address = true;
        config.TunModeItem.IPv6Address = "fc00::172:18:0:1/126";
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var resolvedPool = TunTakeoverPolicy.ResolveNonOverlappingFakeIpv6Range(config);
        await resolvedPool.Should().BeEqualTo("fd00:198:18::/48");
        await IPNetwork2.Parse(config.TunModeItem.IPv6Address).Overlap(IPNetwork2.Parse(resolvedPool)).Should().BeFalse();

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;
        var fakeServer = cfg.dns!.servers.First(s => s.tag == Global.SingboxFakeDNSTag);
        await fakeServer.inet6_range.Should().BeEqualTo("fd00:198:18::/48");
    }

    [Test]
    public async Task SingboxTun_Ipv6Disabled_SuppressesAaaaQueries_AndOmitsInet6FakeRange()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.EnableIPv6Address = false;
        config.SimpleDNSItem.BlockAAAAQuery = false;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
        var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

        var tun = cfg.inbounds.First(i => i.type == "tun");
        await tun.address!.Should().All(a => !a.Contains(':'));

        var fakeServer = cfg.dns!.servers.First(s => s.tag == Global.SingboxFakeDNSTag);
        await fakeServer.inet4_range.Should().BeEqualTo("198.18.0.0/15");
        await fakeServer.inet6_range.Should().BeNull();

        var aaaaBlockIdx = cfg.dns.rules.FindIndex(r =>
            r.action == "predefined"
            && r.rcode == "NOERROR"
            && r.query_type != null
            && r.query_type.Count == 1
            && r.query_type.Contains(28));
        var fakeRuleIdx = cfg.dns.rules.FindIndex(r => r.server == Global.SingboxFakeDNSTag);

        await aaaaBlockIdx.Should().BeGreaterThanOrEqualTo(0);
        await fakeRuleIdx.Should().BeGreaterThan(aaaaBlockIdx);
        await cfg.dns.rules[fakeRuleIdx].rules![0].query_type.Should().Contain(1);
        await cfg.dns.rules[fakeRuleIdx].rules![0].query_type.Should().NotContain(28);
    }

    [Test]
    public async Task SingboxTun_EncryptedDnsGuard_BlocksTunDoTAndPublicDoH_BeforeWhiteRules_WhileExemptingCoreProcess()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.BlockEncryptedDnsBypass = true;
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var xrayInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray)!;
        var stubCorePath = Utils.GetBinPath(Utils.GetExeName(xrayInfo.CoreExes!.First()), ECoreType.Xray.ToString());
        var createdStub = false;
        if (!File.Exists(stubCorePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stubCorePath)!);
            await File.WriteAllBytesAsync(stubCorePath, []);
            createdStub = true;
        }

        try
        {
            var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.sing_box);
            var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.sing_box) with
            {
                IsTunEnabled = true,
                ProtectCoreTypeList = [ECoreType.sing_box, ECoreType.Xray],
                RoutingItem = new RoutingItem
                {
                    Id = "r-white",
                    Remarks = "white",
                    RuleSet = JsonUtils.Serialize(new List<RulesItem>
                    {
                        new()
                        {
                            Enabled = true,
                            RuleType = ERuleType.Routing,
                            OutboundTag = Global.DirectTag,
                            Ip = ["223.5.5.5", "1.1.1.1"],
                        },
                        new()
                        {
                            Enabled = true,
                            RuleType = ERuleType.Routing,
                            OutboundTag = Global.DirectTag,
                            Domain = ["geosite:cn"],
                        },
                    }),
                    DomainStrategy = Global.AsIs,
                    DomainStrategy4Singbox = string.Empty,
                },
            };

            var result = new CoreConfigSingboxService(context).GenerateClientConfigContent();
            await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");
            var cfg = JsonUtils.Deserialize<SingboxConfig>(result.Data!.ToString())!;

            var rules = cfg.route.rules;
            var coreDirectIdx = rules.FindIndex(r => r.outbound == Global.DirectTag && r.process_path is { Count: > 0 });
            var dotGuardIdx = rules.FindIndex(r =>
                r.action == "reject"
                && r.inbound != null && r.inbound.Contains("tun")
                && r.port != null && r.port.Contains(853));
            var dohIpGuardIdx = rules.FindIndex(r =>
                r.action == "reject"
                && r.inbound != null && r.inbound.Contains("tun")
                && r.port != null && r.port.Contains(443)
                && r.ip_cidr != null && r.ip_cidr.Contains("223.5.5.5/32") && r.ip_cidr.Contains("1.1.1.1/32"));
            var dohDomainGuardIdx = rules.FindIndex(r =>
                r.action == "reject"
                && r.inbound != null && r.inbound.Contains("tun")
                && r.port != null && r.port.Contains(443)
                && r.domain_suffix != null
                && r.domain_suffix.Contains("dns.alidns.com")
                && r.domain_suffix.Contains("cloudflare-dns.com"));
            var userDirectIpIdx = rules.FindIndex(r =>
                r.outbound == Global.DirectTag
                && r.ip_cidr != null && r.ip_cidr.Contains("223.5.5.5"));

            await coreDirectIdx.Should().BeGreaterThanOrEqualTo(0);
            await dotGuardIdx.Should().BeGreaterThan(coreDirectIdx);
            await dohIpGuardIdx.Should().BeGreaterThan(coreDirectIdx);
            await dohDomainGuardIdx.Should().BeGreaterThan(coreDirectIdx);

            // Guard rules must NOT combine ip_cidr and domain_suffix on the same rule object (AND semantics)
            await rules[dohIpGuardIdx].domain_suffix.Should().BeNull();
            await rules[dohDomainGuardIdx].ip_cidr.Should().BeNull();

            await userDirectIpIdx.Should().BeGreaterThan(dohIpGuardIdx);
            await userDirectIpIdx.Should().BeGreaterThan(dohDomainGuardIdx);
        }
        finally
        {
            if (createdStub && File.Exists(stubCorePath))
            {
                File.Delete(stubCorePath);
            }
        }
    }

    [Test]
    public async Task V2rayTun_DeepCopiesSniffing_AndUsesFakeDnsPlusOthersWhenRouteOnly()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.AutoFakeIPInTun = true;
        config.TunModeItem.BlockEncryptedDnsBypass = true;
        config.SimpleDNSItem.FakeIP = false;
        config.Inbound.First().SniffingEnabled = true;
        config.Inbound.First().RouteOnly = false;
        config.Inbound.First().DestOverride = ["http", "tls"];
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var node = CoreConfigTestFactory.CreateVmessNode(ECoreType.Xray);
        var context = CoreConfigTestFactory.CreateContext(config, node, ECoreType.Xray) with
        {
            IsTunEnabled = true,
        };

        var result = new CoreConfigV2rayService(context).GenerateClientConfigContent();
        await result.Success.Should().BeTrue().Because($"ret msg: {result.Msg}");

        using var doc = JsonDocument.Parse(result.Data!.ToString()!);
        var root = doc.RootElement;

        var inbounds = root.GetProperty("inbounds").EnumerateArray().ToList();
        var socksInbound = inbounds.First(i => i.GetProperty("tag").GetString() == nameof(EInboundProtocol.socks));
        var tunInbound = inbounds.First(i => i.GetProperty("tag").GetString() == "tun");

        var socksSniffing = socksInbound.GetProperty("sniffing");
        var tunSniffing = tunInbound.GetProperty("sniffing");
        var socksDestOverride = socksSniffing.GetProperty("destOverride")
            .EnumerateArray().Select(x => x.GetString()!).ToList();
        var tunDestOverride = tunSniffing.GetProperty("destOverride")
            .EnumerateArray().Select(x => x.GetString()!).ToList();

        await socksSniffing.GetProperty("routeOnly").GetBoolean().Should().BeFalse();
        await socksDestOverride.Should().Contain("fakedns");
        await socksDestOverride.Should().NotContain("fakedns+others");

        await tunSniffing.GetProperty("routeOnly").GetBoolean().Should().BeTrue();
        await tunDestOverride.Should().Contain("fakedns+others");

        var fakePools = root.GetProperty("fakedns").EnumerateArray().ToList();
        await fakePools.Count.Should().BeEqualTo(2);
        await fakePools[0].GetProperty("ipPool").GetString().Should().BeEqualTo("198.18.0.0/15");
        await fakePools[1].GetProperty("ipPool").GetString().Should().BeEqualTo("fc00::/18");
        await fakePools[1].GetProperty("poolSize").GetInt64().Should().BeEqualTo(65535L);

        var routingRules = root.GetProperty("routing").GetProperty("rules").EnumerateArray().ToList();
        var hasTunBlock853 = routingRules.Any(r =>
            r.TryGetProperty("outboundTag", out var ob) && ob.GetString() == Global.BlockTag
            && r.TryGetProperty("port", out var p) && p.GetString() == "853"
            && r.TryGetProperty("inboundTag", out var ib) && ib.EnumerateArray().Any(x => x.GetString() == "tun"));
        await hasTunBlock853.Should().BeTrue();
    }

    [Test]
    public async Task CoreSelection_StandardVlessNodeInTun_PromotesToSingleCoreSingbox_WithoutPreSocks()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        await EnsureCoreContextSqliteTablesAsync(config);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.PreferSingboxInTun = true;
        config.CoreTypeItem =
        [
            new CoreTypeItem { ConfigType = EConfigType.VLESS, CoreType = ECoreType.Xray }
        ];
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var vlessNode = new ProfileItem
        {
            IndexId = $"vless-tun-{Guid.NewGuid():N}",
            ConfigType = EConfigType.VLESS,
            CoreType = null,
            Remarks = "vless-reality",
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.StreamSecurityReality,
            PublicKey = "test-public-key",
            Sni = "www.microsoft.com",
        };
        vlessNode.SetProtocolExtra(vlessNode.GetProtocolExtra() with
        {
            Flow = "xtls-rprx-vision",
            VlessEncryption = Global.None,
        });

        var allResult = await CoreConfigContextBuilder.BuildAll(config, vlessNode);

        await allResult.Success.Should().BeTrue();
        await allResult.PreSocksResult.Should().BeNull();
        await allResult.MainResult.Context.RunCoreType.Should().BeEqualTo(ECoreType.sing_box);
        await allResult.MainResult.Context.IsTunEnabled.Should().BeTrue();
    }

    [Test]
    public async Task CoreSelection_XhttpOrExplicitXrayNodeInTun_FallsBackToPreSocks()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.Xray);
        await EnsureCoreContextSqliteTablesAsync(config);
        config.TunModeItem.EnableTun = true;
        config.TunModeItem.PreferSingboxInTun = true;
        config.CoreTypeItem =
        [
            new CoreTypeItem { ConfigType = EConfigType.VLESS, CoreType = ECoreType.Xray }
        ];
        CoreConfigTestFactory.BindAppManagerConfig(config);

        var xhttpNode = new ProfileItem
        {
            IndexId = $"vless-xhttp-{Guid.NewGuid():N}",
            ConfigType = EConfigType.VLESS,
            CoreType = null,
            Remarks = "vless-xhttp",
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.xhttp),
            StreamSecurity = Global.StreamSecurity,
        };

        var xhttpAllResult = await CoreConfigContextBuilder.BuildAll(config, xhttpNode);
        await xhttpAllResult.Success.Should().BeTrue();
        await xhttpAllResult.MainResult.Context.RunCoreType.Should().BeEqualTo(ECoreType.Xray);
        await xhttpAllResult.MainResult.Context.IsTunEnabled.Should().BeFalse();
        await xhttpAllResult.PreSocksResult.Should().NotBeNull();
        await xhttpAllResult.PreSocksResult!.Context.RunCoreType.Should().BeEqualTo(ECoreType.sing_box);
        await xhttpAllResult.PreSocksResult.Context.IsTunEnabled.Should().BeTrue();

        var explicitXrayNode = new ProfileItem
        {
            IndexId = $"vless-explicit-xray-{Guid.NewGuid():N}",
            ConfigType = EConfigType.VLESS,
            CoreType = ECoreType.Xray,
            Remarks = "vless-explicit-xray",
            Address = "example.com",
            Port = 443,
            Password = Guid.NewGuid().ToString(),
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.StreamSecurity,
        };

        var explicitAllResult = await CoreConfigContextBuilder.BuildAll(config, explicitXrayNode);
        await explicitAllResult.Success.Should().BeTrue();
        await explicitAllResult.MainResult.Context.RunCoreType.Should().BeEqualTo(ECoreType.Xray);
        await explicitAllResult.PreSocksResult.Should().NotBeNull();
        await explicitAllResult.PreSocksResult!.Context.RunCoreType.Should().BeEqualTo(ECoreType.sing_box);
    }

    [Test]
    public async Task SysProxyCoordination_ToggleTun_ClearsAndRestoresSysProxy_WhilePreservingFirstRunOnboarding()
    {
        var config = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedChange;
        config.TunModeItem.EnableTun = false;
        config.TunModeItem.ClearSysProxyWhenTun = true;

        // First-run / non-TUN onboarding invariant: ForcedChange is preserved
        await TunTakeoverPolicy.GetDesiredSysProxyForWorkflow(config).Should().BeEqualTo(ESysProxyType.ForcedChange);
        await TunTakeoverPolicy.IsEffectiveTakeoverActive(config).Should().BeTrue();

        // Enable TUN -> clears system proxy and records previous state
        var enablePlan = TunTakeoverPolicy.PlanTunSysProxyTransition(config, enableTun: true);
        await enablePlan.TargetSysProxy.Should().BeEqualTo(ESysProxyType.ForcedClear);
        await enablePlan.SavedBeforeTun.Should().BeEqualTo(ESysProxyType.ForcedChange);

        config.TunModeItem.EnableTun = true;
        config.SystemProxyItem.SysProxyType = enablePlan.TargetSysProxy!.Value;
        TunTakeoverPolicy.CommitTunSysProxyTransition(config, enableTun: true, enablePlan.SavedBeforeTun);

        await config.TunModeItem.LastSysProxyBeforeTun.Should().BeEqualTo(ESysProxyType.ForcedChange);
        await TunTakeoverPolicy.GetDesiredSysProxyForWorkflow(config).Should().BeEqualTo(ESysProxyType.ForcedClear);
        await TunTakeoverPolicy.IsEffectiveTakeoverActive(config).Should().BeTrue();
        await TunTakeoverPolicy.FormatRuntimeProxyStatus(config).Should().BeEqualTo("TUN 虚拟网卡接管");

        // Disable TUN -> restores previous ForcedChange system proxy
        var disablePlan = TunTakeoverPolicy.PlanTunSysProxyTransition(config, enableTun: false);
        await disablePlan.TargetSysProxy.Should().BeEqualTo(ESysProxyType.ForcedChange);

        config.TunModeItem.EnableTun = false;
        config.SystemProxyItem.SysProxyType = disablePlan.TargetSysProxy!.Value;
        TunTakeoverPolicy.CommitTunSysProxyTransition(config, enableTun: false, disablePlan.SavedBeforeTun);

        await config.TunModeItem.LastSysProxyBeforeTun.Should().BeNull();
        await config.SystemProxyItem.SysProxyType.Should().BeEqualTo(ESysProxyType.ForcedChange);

        // Non-elevated restart recovery when previous session exited in TUN mode
        config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedClear;
        config.TunModeItem.LastSysProxyBeforeTun = ESysProxyType.Pac;
        TunTakeoverPolicy.RecoverStartupSysProxyWhenTunUnavailable(config);
        await config.SystemProxyItem.SysProxyType.Should().BeEqualTo(ESysProxyType.Pac);
        await config.TunModeItem.LastSysProxyBeforeTun.Should().BeNull();
    }

    [Test]
    public async Task ConfigMigration_UpgradesLegacyUntouchedTunDefaultsOnce_AndPreservesSubsequentUserEdits()
    {
        var legacyConfig = CoreConfigTestFactory.CreateConfig(ECoreType.sing_box);
        legacyConfig.TunModeItem = new TunModeItem
        {
            EnableTun = false,
            Stack = "gvisor",
            Mtu = 9000,
            EnableIPv6Address = false,
            IPv6Address = "fc00::172:18:0:1/126",
            TunTakeoverSchemaVersion = 0,
        };

        TunTakeoverPolicy.ApplyTunConfigMigration(legacyConfig);

        await legacyConfig.TunModeItem.TunTakeoverSchemaVersion.Should().BeEqualTo(Global.CurrentTunTakeoverSchemaVersion);
        await legacyConfig.TunModeItem.Stack.Should().BeEqualTo("mixed");
        await legacyConfig.TunModeItem.Mtu.Should().BeEqualTo(1500);
        await legacyConfig.TunModeItem.EnableIPv6Address.Should().BeTrue();
        await legacyConfig.TunModeItem.IPv6Address.Should().BeEmpty();

        // Subsequent manual edits after schema v1 must not be overwritten
        legacyConfig.TunModeItem.Stack = "gvisor";
        legacyConfig.TunModeItem.Mtu = 9000;
        legacyConfig.TunModeItem.EnableIPv6Address = false;

        TunTakeoverPolicy.ApplyTunConfigMigration(legacyConfig);

        await legacyConfig.TunModeItem.Stack.Should().BeEqualTo("gvisor");
        await legacyConfig.TunModeItem.Mtu.Should().BeEqualTo(9000);
        await legacyConfig.TunModeItem.EnableIPv6Address.Should().BeFalse();
    }

    private static async Task EnsureCoreContextSqliteTablesAsync(Config config)
    {
        SQLiteHelper.Instance.CreateTable<FullConfigTemplateItem>();
        SQLiteHelper.Instance.CreateTable<DNSItem>();
        SQLiteHelper.Instance.CreateTable<RoutingItem>();
        SQLiteHelper.Instance.CreateTable<SubItem>();
        SQLiteHelper.Instance.CreateTable<ProfileItem>();

        const string routingId = "r-tun-takeover-test";
        await SQLiteHelper.Instance.ReplaceAsync(new RoutingItem
        {
            Id = routingId,
            Remarks = "tun-test",
            RuleSet = "[]",
            DomainStrategy = Global.AsIs,
            DomainStrategy4Singbox = string.Empty,
            Enabled = true,
        });
        config.RoutingBasicItem.RoutingIndexId = routingId;
    }
}
