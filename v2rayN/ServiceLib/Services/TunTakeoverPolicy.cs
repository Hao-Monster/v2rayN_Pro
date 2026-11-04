namespace ServiceLib.Services;

public readonly record struct TunSysProxyTransitionPlan(
    ESysProxyType? TargetSysProxy,
    ESysProxyType? SavedBeforeTun);

/// <summary>
/// Centralizes pure policy decisions for TUN full-traffic takeover:
/// dual-stack IPv6 + AAAA leak suppression, TUN-scoped dual-stack FakeIP,
/// encrypted DNS bypass guard, single-core sing-box promotion, and deterministic
/// coordination between TUN and system proxy.
/// </summary>
public static class TunTakeoverPolicy
{
    public static void ApplyTunConfigMigration(Config config)
    {
        config.TunModeItem ??= new TunModeItem
        {
            EnableTun = false,
            Stack = Global.DefaultTunStack,
            Mtu = Global.DefaultTunMtu,
            EnableIPv6Address = true,
            IcmpRouting = Global.TunIcmpRoutingPolicies.First(),
            EnableLegacyProtect = true,
            AutoFakeIPInTun = true,
            BlockEncryptedDnsBypass = true,
            ClearSysProxyWhenTun = true,
            PreferSingboxInTun = true,
            TunTakeoverSchemaVersion = Global.CurrentTunTakeoverSchemaVersion,
        };

        if (config.TunModeItem.IcmpRouting.IsNullOrEmpty())
        {
            config.TunModeItem.IcmpRouting = Global.TunIcmpRoutingPolicies.First();
        }

        if (config.TunModeItem.TunTakeoverSchemaVersion < Global.CurrentTunTakeoverSchemaVersion)
        {
            config.TunModeItem.EnableIPv6Address = true;

            if (config.TunModeItem.Stack.IsNullOrEmpty()
                || string.Equals(config.TunModeItem.Stack, "gvisor", StringComparison.OrdinalIgnoreCase))
            {
                config.TunModeItem.Stack = Global.DefaultTunStack;
            }

            if (config.TunModeItem.Mtu <= 0 || config.TunModeItem.Mtu == 9000)
            {
                config.TunModeItem.Mtu = Global.DefaultTunMtu;
            }

            if (string.Equals(config.TunModeItem.IPv6Address, "fc00::172:18:0:1/126", StringComparison.OrdinalIgnoreCase))
            {
                config.TunModeItem.IPv6Address = string.Empty;
            }

            config.TunModeItem.TunTakeoverSchemaVersion = Global.CurrentTunTakeoverSchemaVersion;
        }
    }

    /// <summary>
    /// Whether effective network takeover is active (either TUN mode or automatic system proxy).
    /// Used by subscription refresh and latency sorting workflows to decide whether to preserve
    /// the current active node.
    /// </summary>
    public static bool IsEffectiveTakeoverActive(Config config)
    {
        return config.TunModeItem?.EnableTun == true
            || config.SystemProxyItem?.SysProxyType == ESysProxyType.ForcedChange;
    }

    /// <summary>
    /// Desired system proxy state when automated workflows activate a server or repair connectivity.
    /// Preserves ESysProxyType.ForcedChange whenever TUN is disabled (including first-run onboarding).
    /// </summary>
    public static ESysProxyType GetDesiredSysProxyForWorkflow(Config config)
    {
        if (config.TunModeItem is { EnableTun: true })
        {
            return config.TunModeItem.ClearSysProxyWhenTun
                ? ESysProxyType.ForcedClear
                : config.SystemProxyItem.SysProxyType;
        }

        return ESysProxyType.ForcedChange;
    }

    /// <summary>
    /// Computes the system proxy transition needed when toggling EnableTun without mutating config yet.
    /// Call <see cref="CommitTunSysProxyTransition" /> once applying the system proxy succeeds.
    /// </summary>
    public static TunSysProxyTransitionPlan PlanTunSysProxyTransition(Config config, bool enableTun)
    {
        if (config.TunModeItem?.ClearSysProxyWhenTun != true)
        {
            return new TunSysProxyTransitionPlan(null, null);
        }

        var currentSysProxy = config.SystemProxyItem.SysProxyType;
        if (enableTun)
        {
            var savedBeforeTun = config.TunModeItem.LastSysProxyBeforeTun ?? currentSysProxy;
            return currentSysProxy != ESysProxyType.ForcedClear
                ? new TunSysProxyTransitionPlan(ESysProxyType.ForcedClear, savedBeforeTun)
                : new TunSysProxyTransitionPlan(null, savedBeforeTun);
        }

        if (currentSysProxy == ESysProxyType.ForcedClear
            && config.TunModeItem.LastSysProxyBeforeTun is { } saved
            && saved != ESysProxyType.ForcedClear)
        {
            return new TunSysProxyTransitionPlan(saved, null);
        }

        return new TunSysProxyTransitionPlan(null, null);
    }

    public static void CommitTunSysProxyTransition(Config config, bool enableTun, ESysProxyType? savedBeforeTun)
    {
        if (config.TunModeItem?.ClearSysProxyWhenTun != true)
        {
            return;
        }

        if (enableTun)
        {
            if (savedBeforeTun is not null)
            {
                config.TunModeItem.LastSysProxyBeforeTun = savedBeforeTun;
            }
        }
        else
        {
            config.TunModeItem.LastSysProxyBeforeTun = null;
        }
    }

    /// <summary>
    /// Computes the live system proxy transition needed when ClearSysProxyWhenTun is toggled
    /// in OptionSettingWindow while EnableTun is already active.
    /// </summary>
    public static TunSysProxyTransitionPlan PlanLiveTunSettingsSysProxySync(Config config)
    {
        if (config.TunModeItem is not { EnableTun: true })
        {
            return new TunSysProxyTransitionPlan(null, null);
        }

        if (config.TunModeItem.ClearSysProxyWhenTun)
        {
            return PlanTunSysProxyTransition(config, enableTun: true);
        }

        if (config.SystemProxyItem.SysProxyType == ESysProxyType.ForcedClear
            && config.TunModeItem.LastSysProxyBeforeTun is { } saved
            && saved != ESysProxyType.ForcedClear)
        {
            return new TunSysProxyTransitionPlan(saved, null);
        }

        return new TunSysProxyTransitionPlan(null, null);
    }

    public static void CommitLiveTunSettingsSysProxySync(Config config, ESysProxyType? savedBeforeTun)
    {
        if (config.TunModeItem is not { EnableTun: true })
        {
            return;
        }

        if (config.TunModeItem.ClearSysProxyWhenTun)
        {
            CommitTunSysProxyTransition(config, enableTun: true, savedBeforeTun);
        }
        else
        {
            config.TunModeItem.LastSysProxyBeforeTun = null;
        }
    }

    /// <summary>
    /// If the previous session exited with TUN enabled and system proxy cleared, but the current
    /// launch does not have elevation to run TUN, restore the saved system proxy mode so the user
    /// is not left with both TUN and system proxy disabled.
    /// </summary>
    public static void RecoverStartupSysProxyWhenTunUnavailable(Config config)
    {
        if (config.TunModeItem?.ClearSysProxyWhenTun == true
            && config.TunModeItem.LastSysProxyBeforeTun is { } saved
            && config.SystemProxyItem.SysProxyType == ESysProxyType.ForcedClear)
        {
            config.SystemProxyItem.SysProxyType = saved;
            config.TunModeItem.LastSysProxyBeforeTun = null;
        }
    }

    public static string FormatRuntimeProxyStatus(Config config)
    {
        var sysProxyType = config.SystemProxyItem?.SysProxyType ?? ESysProxyType.ForcedClear;
        if (config.TunModeItem?.EnableTun == true)
        {
            return sysProxyType switch
            {
                ESysProxyType.ForcedChange => "TUN + 系统代理",
                ESysProxyType.Pac => "TUN + PAC",
                _ => "TUN 虚拟网卡接管",
            };
        }

        return sysProxyType switch
        {
            ESysProxyType.ForcedChange => "自动配置系统代理",
            ESysProxyType.Pac => "PAC 模式",
            ESysProxyType.Unchanged => "保持系统设置",
            _ => "未开启",
        };
    }

    public static bool IsEffectiveFakeIpEnabled(CoreConfigContext context)
    {
        if (context.RawDnsItem is { Enabled: true })
        {
            return false;
        }

        return context.SimpleDnsItem?.FakeIP == true
            || (context.IsTunEnabled && context.AppConfig.TunModeItem?.AutoFakeIPInTun == true);
    }

    public static bool ShouldSuppressIpv6Dns(CoreConfigContext context)
    {
        return context.SimpleDnsItem?.BlockAAAAQuery == true
            || (context.IsTunEnabled && context.AppConfig.TunModeItem?.EnableIPv6Address != true);
    }

    public static string ResolveNonOverlappingFakeIpv6Range(Config config)
    {
        var preferred = config.SimpleDNSItem?.FakeIPv6Range.NullIfEmpty() ?? Global.FakeIPv6Ranges.First();
        var tunIpv6 = config.TunModeItem?.IPv6Address.NullIfEmpty() ?? Global.TunIPv6Address.First();

        try
        {
            var fakeNet = IPNetwork2.Parse(preferred);
            var tunNet = IPNetwork2.Parse(tunIpv6);
            if (!fakeNet.Overlap(tunNet))
            {
                return preferred;
            }

            foreach (var fallback in Global.FakeIPv6Ranges)
            {
                var candidate = IPNetwork2.Parse(fallback);
                if (!candidate.Overlap(tunNet))
                {
                    return fallback;
                }
            }
        }
        catch
        {
            // Ignore malformed user CIDR and fall back to a disjoint ULA prefix.
        }

        return "fd00:198:18::/48";
    }

    /// <summary>
    /// Returns the encrypted DNS / DDR domains that should be blocked for TUN-inbound app queries,
    /// exempting domains protected by active proxy nodes or user Hosts overrides (and optionally
    /// core upstream DNS server domains when scoping by inbound tag is unavailable, e.g. Xray hosts).
    /// </summary>
    public static List<string> GetDnsGuardBlockedDomains(CoreConfigContext context, bool excludeCoreDnsDomains = false)
    {
        var exemptDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var protectedDomain in context.ProtectDomainList)
        {
            AddNormalizedExemptDomain(exemptDomains, protectedDomain);
        }

        if (!string.IsNullOrEmpty(context.SimpleDnsItem?.Hosts))
        {
            foreach (var hostKey in Utils.ParseHostsToDictionary(context.SimpleDnsItem.Hosts).Keys)
            {
                AddNormalizedExemptDomain(exemptDomains, hostKey);
            }
        }

        if (excludeCoreDnsDomains)
        {
            CollectDnsEndpointDomains(
                exemptDomains,
                context.SimpleDnsItem?.DirectDNS,
                Global.DomainDirectDNSAddress.First());
            CollectDnsEndpointDomains(
                exemptDomains,
                context.SimpleDnsItem?.RemoteDNS,
                Global.DomainRemoteDNSAddress.First());
            CollectDnsEndpointDomains(
                exemptDomains,
                context.SimpleDnsItem?.BootstrapDNS,
                Global.DomainPureIPDNSAddress.First());
        }

        if (exemptDomains.Count == 0)
        {
            return [.. Global.TunEncryptedDnsGuardDomains];
        }

        return Global.TunEncryptedDnsGuardDomains
            .Where(guard => !exemptDomains.Any(exempt =>
                string.Equals(guard, exempt, StringComparison.OrdinalIgnoreCase)
                || exempt.EndsWith("." + guard, StringComparison.OrdinalIgnoreCase)
                || guard.EndsWith("." + exempt, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static bool CanRunSingleCoreSingboxInTun(
        Config config,
        ProfileItem node,
        IReadOnlyDictionary<string, ProfileItem>? allProxiesMap = null,
        FullConfigTemplateItem? fullConfigTemplate = null,
        DNSItem? rawDnsItem = null)
    {
        if (config.TunModeItem is not { EnableTun: true, PreferSingboxInTun: true })
        {
            return false;
        }

        if (fullConfigTemplate is { Enabled: true } || rawDnsItem is { Enabled: true })
        {
            return false;
        }

        if ((config.CoreBasicItem?.EnableFragment == true || config.CoreBasicItem?.EnableFinalFragment == true)
            && config.Fragment4RayItem?.Packets is { Length: > 0 } packets
            && !string.Equals(packets, "tlshello", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (node.CoreType is not null && node.CoreType != ECoreType.sing_box)
        {
            return false;
        }

        if (!IsNodeSupportedBySingbox(node))
        {
            return false;
        }

        if (allProxiesMap is { Count: > 0 })
        {
            foreach (var child in allProxiesMap.Values)
            {
                if (child.CoreType is not null && child.CoreType != ECoreType.sing_box)
                {
                    return false;
                }

                if (!IsNodeSupportedBySingbox(child))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool IsNodeSupportedBySingbox(ProfileItem node)
    {
        if (node.ConfigType is EConfigType.Custom or EConfigType.Outbound)
        {
            return node.CoreType == ECoreType.sing_box;
        }

        if (node.ConfigType.IsGroupType())
        {
            return true;
        }

        if (!Global.SingboxSupportConfigType.Contains(node.ConfigType))
        {
            return false;
        }

        if (!node.CertSha.IsNullOrEmpty()
            || !node.Mldsa65Verify.IsNullOrEmpty()
            || !node.VerifyPeerCertByName.IsNullOrEmpty()
            || !node.Finalmask.IsNullOrEmpty())
        {
            return false;
        }

        if (node.ConfigType == EConfigType.SOCKS
            && node.StreamSecurity is Global.StreamSecurity or Global.StreamSecurityReality)
        {
            return false;
        }

        var network = node.GetNetwork();
        if (network is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
        {
            return false;
        }

        var transportExtra = node.GetTransportExtra();
        if (node.ConfigType != EConfigType.Shadowsocks
            && network == nameof(ETransport.raw)
            && string.Equals(transportExtra.RawHeaderType, Global.RawHeaderHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (node.ConfigType is not (EConfigType.VMess or EConfigType.VLESS or EConfigType.Trojan or EConfigType.Shadowsocks)
            && network != nameof(ETransport.raw))
        {
            return false;
        }

        if (node.ConfigType == EConfigType.Shadowsocks
            && network is not (nameof(ETransport.raw) or nameof(ETransport.ws)))
        {
            return false;
        }

        var extra = node.GetProtocolExtra();
        if (node.ConfigType == EConfigType.VLESS)
        {
            if (!extra.VlessEncryption.IsNullOrEmpty()
                && !string.Equals(extra.VlessEncryption, Global.None, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(extra.Flow, "xtls-rprx-vision-udp443", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        else if (node.ConfigType == EConfigType.VMess)
        {
            if (!extra.VmessSecurity.IsNullOrEmpty() && !Global.VmessSecurities.Contains(extra.VmessSecurity))
            {
                return false;
            }
        }
        else if (node.ConfigType == EConfigType.Shadowsocks)
        {
            if (extra.SsMethod.IsNullOrEmpty() || !Global.SsSecuritiesInSingbox.Contains(extra.SsMethod))
            {
                return false;
            }
        }
        else if (node.ConfigType == EConfigType.HTTP)
        {
            if (!extra.HttpHeaders.IsNullOrEmpty())
            {
                return false;
            }
        }

        return NodeValidator.Validate(node, ECoreType.sing_box).Success;
    }

    private static void CollectDnsEndpointDomains(HashSet<string> target, string? dnsInput, string defaultAddress)
    {
        var addresses = (Utils.String2List(dnsInput) ?? [])
            .Select(addr => addr.Trim())
            .Where(addr => !string.IsNullOrEmpty(addr))
            .ToList();
        if (addresses.Count == 0)
        {
            addresses.Add(defaultAddress);
        }

        foreach (var addr in addresses)
        {
            var (domain, _, _, _) = Utils.ParseUrl(addr);
            if (!string.IsNullOrEmpty(domain)
                && !string.Equals(domain, "localhost", StringComparison.OrdinalIgnoreCase)
                && Utils.IsDomain(domain))
            {
                AddNormalizedExemptDomain(target, domain);
            }
        }
    }

    private static void AddNormalizedExemptDomain(HashSet<string> target, string? rawDomain)
    {
        if (string.IsNullOrWhiteSpace(rawDomain))
        {
            return;
        }

        var normalized = rawDomain.Trim();
        var colonIdx = normalized.IndexOf(':');
        if (colonIdx >= 0 && colonIdx + 1 < normalized.Length)
        {
            normalized = normalized[(colonIdx + 1)..];
        }

        normalized = normalized.Trim().Trim('.');
        if (!string.IsNullOrEmpty(normalized))
        {
            target.Add(normalized);
        }
    }
}
