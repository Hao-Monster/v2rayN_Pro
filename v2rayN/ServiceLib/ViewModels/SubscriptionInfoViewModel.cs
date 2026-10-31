namespace ServiceLib.ViewModels;

public partial class SubscriptionInfoViewModel : MyReactiveObject
{
    [Reactive]
    public partial string SubscriptionName { get; set; } = "订阅信息";

    [Reactive]
    public partial string Announcement { get; set; } = "选择一个订阅分组以查看订阅信息";

    [Reactive]
    public partial string RemainingTraffic { get; set; } = "-";

    [Reactive]
    public partial string TotalTraffic { get; set; } = "-";

    [Reactive]
    public partial string UsedTraffic { get; set; } = "-";

    [Reactive]
    public partial string ExpireDate { get; set; } = "-";

    [Reactive]
    public partial string LastUpdated { get; set; } = "尚未更新";

    [Reactive]
    public partial bool HasSubscription { get; set; }

    [Reactive]
    public partial string SupportUrl { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasSupportUrl { get; set; }

    [Reactive]
    public partial string NetworkStatusText { get; set; } = "正在检查当前网络";

    [Reactive]
    public partial string NetworkStatusDetail { get; set; } = string.Empty;

    [Reactive]
    public partial string NetworkStatusColor { get; set; } = "#F9A825";

    [Reactive]
    public partial bool IsNetworkRepairing { get; set; }

    [Reactive]
    public partial bool CanContactSupport { get; set; }

    [Reactive]
    public partial string ActiveNodeName { get; set; } = "未选择节点";

    [Reactive]
    public partial string CurrentDelay { get; set; } = "未测速";

    [Reactive]
    public partial string SystemProxyStatus { get; set; } = "未开启";

    public ReactiveCommand<RxVoid, RxVoid> OpenSupportCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> OneClickNetworkSetupCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ContactSupportCmd { get; }

    public SubscriptionInfoViewModel()
    {
        _config = AppManager.Instance.Config;
        OpenSupportCmd = ReactiveCommand.Create(
            () => ProcUtils.ProcessStart(SupportUrl),
            this.WhenAnyValue(x => x.HasSupportUrl));
        OneClickNetworkSetupCmd = ReactiveCommand.Create(
            () => AppEvents.OneClickNetworkSetupRequested.Publish(),
            this.WhenAnyValue(x => x.IsNetworkRepairing, repairing => !repairing));
        ContactSupportCmd = ReactiveCommand.Create(() =>
        {
            if (HasSupportUrl)
            {
                ProcUtils.ProcessStart(SupportUrl);
            }
            else
            {
                AppEvents.EditCurrentSubscriptionRequested.Publish();
            }
        });

        AppEvents.SubscriptionSelectionChanged
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async subId => await Refresh(subId));

        AppEvents.SubscriptionsRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await Refresh(_config.SubIndexId));
        AppEvents.ProfilesRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshRuntimeStatus());
        AppEvents.NetworkAvailabilityChanged
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async info =>
            {
                ApplyNetworkStatus(info);
                await RefreshRuntimeStatus();
            });

        StatusBarViewModel.Instance
            .WhenAnyValue(x => x.SystemProxySelected)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshRuntimeStatus());
        StatusBarViewModel.Instance
            .WhenAnyValue(x => x.SelectedServer)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshRuntimeStatus());

        _ = Refresh(_config.SubIndexId);
    }

    private void ApplyNetworkStatus(NetworkAvailabilityInfo info)
    {
        NetworkStatusText = info.Message;
        NetworkStatusDetail = info.Detail;
        IsNetworkRepairing = info.State is ENetworkAvailabilityState.Checking or ENetworkAvailabilityState.Repairing;
        CanContactSupport = info.State == ENetworkAvailabilityState.NoAvailableNode;
        NetworkStatusColor = info.State switch
        {
            ENetworkAvailabilityState.Available => "#2E7D32",
            ENetworkAvailabilityState.Repairing or ENetworkAvailabilityState.Checking => "#F9A825",
            ENetworkAvailabilityState.Repaired => "#1976D2",
            ENetworkAvailabilityState.NoAvailableNode => "#C62828",
            ENetworkAvailabilityState.LocalNetworkUnavailable => "#616161",
            _ => "#616161"
        };
    }

    private async Task Refresh(string? subId)
    {
        var item = subId.IsNotEmpty()
            ? await AppManager.Instance.GetSubItem(subId)
            : null;

        if (item is null)
        {
            HasSubscription = false;
            SubscriptionName = "订阅信息";
            Announcement = "选择一个订阅分组以查看订阅信息";
            RemainingTraffic = "-";
            TotalTraffic = "-";
            UsedTraffic = "-";
            ExpireDate = "-";
            LastUpdated = "尚未更新";
            SupportUrl = string.Empty;
            HasSupportUrl = false;
            await RefreshRuntimeStatus();
            return;
        }

        HasSubscription = true;
        SubscriptionName = item.Remarks.IsNotEmpty() ? item.Remarks : "未命名订阅";
        Announcement = item.Announce.IsNotEmpty() ? item.Announce! : "暂无订阅公告";

        var usedTraffic = Math.Max(0, item.TrafficUpload) + Math.Max(0, item.TrafficDownload);
        TotalTraffic = item.TrafficTotal > 0 ? FormatGigabytes(item.TrafficTotal) : "∞";
        UsedTraffic = FormatGigabytes(usedTraffic);
        RemainingTraffic = item.TrafficTotal > 0
            ? FormatGigabytes(Math.Max(0, item.TrafficTotal - usedTraffic))
            : "∞";

        if (item.ExpireTime > 0)
        {
            try
            {
                var expire = DateTimeOffset.FromUnixTimeSeconds(item.ExpireTime).ToLocalTime();
                ExpireDate = expire.ToString("yyyy-MM-dd");
            }
            catch (ArgumentOutOfRangeException)
            {
                ExpireDate = "-";
            }
        }
        else
        {
            ExpireDate = "永久";
        }

        LastUpdated = item.UpdateTime > 0
            ? $"更新于 {DateTimeOffset.FromUnixTimeSeconds(item.UpdateTime).ToLocalTime():yyyy-MM-dd HH:mm}"
            : "尚未更新";

        SupportUrl = NormalizeSupportUrl(item.SupportUrl);
        HasSupportUrl = SupportUrl.IsNotEmpty();
        await RefreshRuntimeStatus();
    }

    private async Task RefreshRuntimeStatus()
    {
        var profile = _config.IndexId.IsNotEmpty()
            ? await AppManager.Instance.GetProfileItem(_config.IndexId)
            : null;

        ActiveNodeName = profile?.Remarks.IsNotEmpty() == true
            ? profile.Remarks
            : "未选择节点";

        var profileExs = await ProfileExManager.Instance.GetProfileExs();
        var delay = profileExs.FirstOrDefault(item => item.IndexId == _config.IndexId)?.Delay ?? 0;
        CurrentDelay = delay switch
        {
            > 0 => $"{delay} ms",
            < 0 => "不可用",
            _ => "未测速"
        };

        SystemProxyStatus = _config.SystemProxyItem.SysProxyType switch
        {
            ESysProxyType.ForcedChange => "自动配置系统代理",
            ESysProxyType.Pac => "PAC 模式",
            ESysProxyType.Unchanged => "保持系统设置",
            _ => "未开启"
        };
    }

    private static string NormalizeSupportUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Scheme is "http" or "https" or "tg" ? uri.AbsoluteUri : string.Empty;
    }

    private static string FormatGigabytes(long bytes)
    {
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / bytesPerGigabyte:#,##0.##} GB";
    }
}
