namespace ServiceLib.Common;

internal readonly record struct NetworkRecoveryEligibility(bool HasSubscription, bool HasProfiles)
{
    public bool CanMonitor => HasSubscription && HasProfiles;
    public bool CanSetUp => HasSubscription || HasProfiles;

    public string IdleMessage => (HasSubscription, HasProfiles) switch
    {
        (false, false) => "暂无订阅",
        (true, false) => "订阅暂无节点",
        (false, true) => "已配置本地节点",
        _ => "尚未检测网络"
    };

    public static bool AcceptsStatus(NetworkRecoveryEligibility? configuration, NetworkAvailabilityInfo status, long currentRevision)
    {
        if (status.MonitorRevision is long revision)
        {
            return revision == currentRevision && configuration?.CanMonitor != false;
        }
        return configuration?.CanSetUp != false || status.State == ENetworkAvailabilityState.Idle;
    }

    public bool ShouldKeepRuntimeStatus(NetworkRecoveryEligibility? previous, bool setupRunning)
    {
        if (!CanSetUp)
        {
            return false;
        }

        // A subscription download can add nodes before or after the final status reaches the UI.
        return setupRunning || previous is null || (previous.Value.HasSubscription && HasSubscription && HasProfiles);
    }

    public static async Task<NetworkRecoveryEligibility> ReadAsync()
    {
        var subscriptions = await AppManager.Instance.SubItems();
        var hasSubscription = subscriptions?.Any(item => item.Enabled
            && SubscriptionHandler.IsValidSubscription(item, string.Empty)) == true;
        var profile = await SQLiteHelper.Instance.TableAsync<ProfileItem>().FirstOrDefaultAsync();
        return new(hasSubscription, profile is not null);
    }
}
