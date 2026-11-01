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
            return currentSysProxy != ESysProxyType.ForcedClear
                ? new TunSysProxyTransitionPlan(ESysProxyType.ForcedClear, currentSysProxy)
                : new TunSysProxyTransitionPlan(null, config.TunModeItem.LastSysProxyBeforeTun);
        }

        var restore = config.TunModeItem.LastSysProxyBeforeTun ?? ESysProxyType.ForcedChange;
        return currentSysProxy == ESysProxyType.ForcedClear
            ? new TunSysProxyTransitionPlan(restore, null)
            : new TunSysProxyTransitionPlan(null, null);
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

    public static bool CanRunSingleCoreSingboxInTun(
        Config config,
        ProfileItem node,
        IReadOnlyDictionary<string, ProfileItem>? allProxiesMap = null)
    {
        if (config.TunModeItem is not { EnableTun: true, PreferSingboxInTun: true })
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

        var network = node.GetNetwork();
        if (network is nameof(ETransport.kcp) or nameof(ETransport.xhttp))
        {
            return false;
        }

        if (!node.Finalmask.IsNullOrEmpty())
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

        if (node.ConfigType == EConfigType.VLESS)
        {
            var extra = node.GetProtocolExtra();
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
        else if (node.ConfigType == EConfigType.Shadowsocks)
        {
            var ssMethod = node.GetProtocolExtra().SsMethod;
            if (!ssMethod.IsNullOrEmpty() && !Global.SsSecuritiesInSingbox.Contains(ssMethod))
            {
                return false;
            }
        }

        return true;
    }
}
