namespace ServiceLib.ViewModels;

public partial class MainWindowViewModel : MyReactiveObject
{
    public Interaction<RxVoid, string?> ReadTextFromClipboardInteraction { get; } = new();
    public Interaction<RxVoid, byte[]?> ScanScreenInteraction { get; } = new();
    public Interaction<RxVoid, string?> BrowseImageFileInteraction { get; } = new();
    public Interaction<bool?, RxVoid> ShowHideWindowInteraction { get; } = new();

    public bool DesignMode { get; set; }

    public ProfilesViewModel ProfilesViewModel { get; } = new();
    public MsgViewModel MsgViewModel { get; } = new();
    public ClashProxiesViewModel ClashProxiesViewModel { get; } = new();
    public ClashConnectionsViewModel ClashConnectionsViewModel { get; } = new();
    public CheckUpdateViewModel CheckUpdateViewModel { get; } = new();
    public BackupAndRestoreViewModel BackupAndRestoreViewModel { get; } = new();
    public StatusBarViewModel StatusBarViewModel { get; } = StatusBarViewModel.Instance;

    private const int SlowNetworkDelay = 500;
    private const int AutomaticRepairFailureThreshold = 3;
    private static readonly TimeSpan RepairedStatusDuration = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _subscriptionWorkflowSemaphore = new(1, 1);
    private readonly SemaphoreSlim _networkRepairSemaphore = new(1, 1);
    private readonly CancellationTokenSource _networkHealthCts = new();
    private int _consecutiveNetworkFailures;
    private DateTimeOffset? _lastNetworkRepairAt;
    private bool _networkHealthMonitorEnabled;

    #region Menu

    //servers
    public ReactiveCommand<RxVoid, RxVoid> AddVmessServerCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddVlessServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddShadowsocksServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddSocksServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddHttpServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddTrojanServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddHysteria2ServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddTuicServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddWireguardServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddAnytlsServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddNaiveServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddMasqueServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddCustomServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddCustomOutboundServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddPolicyGroupServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddProxyChainServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaClipboardCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaScanCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> AddServerViaImageCmd { get; }

    //Subscription
    public ReactiveCommand<RxVoid, RxVoid> SubSettingCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> SubUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubUpdateViaProxyCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubGroupUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubGroupUpdateViaProxyCmd { get; }

    //Setting
    public ReactiveCommand<RxVoid, RxVoid> OptionSettingCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RoutingSettingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DNSSettingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FullConfigTemplateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> GlobalHotkeySettingCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RebootAsAdminCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ClearServerStatisticsCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenTheFileLocationCmd { get; }

    //Presets
    public ReactiveCommand<RxVoid, RxVoid> RegionalPresetDefaultCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RegionalPresetRussiaCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RegionalPresetIranCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReloadCmd { get; }

    [Reactive]
    public partial bool BlReloadEnabled { get; set; }

    [Reactive]
    public partial bool ShowClashUI { get; set; }

    [Reactive]
    public partial int TabMainSelectedIndex { get; set; }

    [Reactive] public partial bool BlIsWindows { get; set; }

    [Reactive] public partial bool BlNewUpdate { get; set; }

    [Reactive] public partial EGirdOrientation MainGirdOrientation { get; set; }

    #endregion Menu

    #region Init

    public MainWindowViewModel()
    {
        _config = AppManager.Instance.Config;
        BlIsWindows = Utils.IsWindows();
        MainGirdOrientation = _config.UiItem.MainGirdOrientation;

        #region WhenAnyValue && ReactiveCommand

        //servers
        AddVmessServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.VMess);
        });
        AddVlessServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.VLESS);
        });
        AddShadowsocksServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Shadowsocks);
        });
        AddSocksServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.SOCKS);
        });
        AddHttpServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.HTTP);
        });
        AddTrojanServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Trojan);
        });
        AddHysteria2ServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Hysteria2);
        });
        AddTuicServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.TUIC);
        });
        AddWireguardServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.WireGuard);
        });
        AddAnytlsServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Anytls);
        });
        AddNaiveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Naive);
        });
        AddMasqueServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.MASQUE);
        });
        AddCustomServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Custom);
        });
        AddCustomOutboundServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.Outbound);
        });
        AddPolicyGroupServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.PolicyGroup);
        });
        AddProxyChainServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerAsync(EConfigType.ProxyChain);
        });
        AddServerViaClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaClipboardAsync(null);
        });
        AddServerViaScanCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaScanAsync();
        });
        AddServerViaImageCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaImageAsync();
        });

        //Subscription
        SubSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SubSettingAsync();
        });

        SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess("", false);
        });
        SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess("", true);
        });
        SubGroupUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(_config.SubIndexId, false);
        });
        SubGroupUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(_config.SubIndexId, true);
        });

        //Setting
        OptionSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OptionSettingAsync();
        });
        RoutingSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RoutingSettingAsync();
        });
        DNSSettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DNSSettingAsync();
        });
        FullConfigTemplateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await FullConfigTemplateAsync();
        });
        GlobalHotkeySettingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            var globalHotkeySettingViewModel = new GlobalHotkeySettingViewModel();
            if (await AppManager.Instance.WindowDialog.ShowDialogAsync(globalHotkeySettingViewModel) == true)
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            }
        });
        RebootAsAdminCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AppManager.Instance.RebootAsAdmin();
        });
        ClearServerStatisticsCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ClearServerStatistics();
        });
        OpenTheFileLocationCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OpenTheFileLocation();
        });

        ReloadCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Reload();
        });

        RegionalPresetDefaultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyRegionalPreset(EPresetType.Default);
        });

        RegionalPresetRussiaCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyRegionalPreset(EPresetType.Russia);
        });

        RegionalPresetIranCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ApplyRegionalPreset(EPresetType.Iran);
        });

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.AddServerViaClipboardRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await AddServerViaClipboardAsync(null));

        AppEvents.SubscriptionsUpdateRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async blProxy => await UpdateSubscriptionProcess("", blProxy));

        AppEvents.CurrentSubscriptionUpdateRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async subId => await UpdateSubscriptionProcess(subId, false));
        AppEvents.OneClickNetworkSetupRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RunNetworkSetup(false));
        AppEvents.EditCurrentSubscriptionRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await ProfilesViewModel.EditCurrentSubscription());
        AppEvents.AppExitRequested
            .AsObservable()
            .Subscribe(_ => _networkHealthCts.Cancel());
        AppEvents.HasUpdateNotified
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(bl => BlNewUpdate = bl);

        #endregion AppEvents

        ProfilesViewModel.RefreshServersRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await RefreshServersDispatcherAsync());

        var vmReloadRequestedList = new List<IObservable<RxVoid>>
        {
            ProfilesViewModel.ReloadRequested.AsObservable(),
            StatusBarViewModel.ReloadRequested.AsObservable(),
            CheckUpdateViewModel.ReloadRequested.AsObservable(),
        };

        foreach (var reloadRequested in vmReloadRequestedList)
        {
            reloadRequested
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SubscribeAsync(async _ => await Reload());
        }

        StatusBarViewModel.AddServerViaScanRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await AddServerViaScanAsync());

        StatusBarViewModel.AddServerViaClipboardRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async _ => await AddServerViaClipboardAsync(null));

        StatusBarViewModel.ShowHideWindowRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async blShow =>
            {
                await ShowHideWindowInteraction.HandleSafe(blShow);
            });

        StatusBarViewModel.SetDefaultServerRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async indexId => await ProfilesViewModel.SetDefaultServer(indexId));

        StatusBarViewModel.SubscriptionsUpdateRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async blProxy => await UpdateSubscriptionProcess("", blProxy));

        _ = Init();
    }

    private async Task Init()
    {
        AppManager.Instance.ShowInTaskbar = true;

        if (DesignMode)
        {
            return;
        }

        //await ConfigHandler.InitBuiltinRouting(_config);
        await ConfigHandler.InitBuiltinDNS(_config);
        await ConfigHandler.InitBuiltinFullConfigTemplate(_config);
        await ProfileExManager.Instance.Init();
        await CoreManager.Instance.Init(_config, UpdateHandler);
        await CertPemManager.Instance.Init(_config);
        TaskManager.Instance.RegUpdateTask(_config, UpdateTaskHandler, UpdateSubscriptionProcess);

        if (_config.GuiItem.EnableStatistics || _config.GuiItem.DisplayRealTimeSpeed)
        {
            await StatisticsManager.Instance.Init(_config, UpdateStatisticsHandler);
        }
        await RefreshServersDispatcherAsync();

        _networkHealthMonitorEnabled = Utils.IsWindows()
            && !DesignMode
            && await HasExistingSubscriptionConfiguration();
        await Reload();
        StartNetworkHealthMonitor();
    }

    #endregion Init

    #region Actions

    private async Task UpdateHandler(bool notify, string msg)
    {
        NoticeManager.Instance.SendMessage(msg);
        if (notify)
        {
            NoticeManager.Instance.Enqueue(msg);
        }
        await Task.CompletedTask;
    }

    private async Task UpdateTaskHandler(bool success, string msg)
    {
        NoticeManager.Instance.SendMessageEx(msg);
        if (success)
        {
            var indexIdOld = _config.IndexId;
            await RefreshServersDispatcherAsync();

            // If indexId changed or subIndexId is empty, directly reload.
            if (indexIdOld != _config.IndexId || _config.SubIndexId.IsNullOrEmpty())
            {
                await Reload();
            }
            else
            {
                // The activity config belongs to the current group.
                var profile = await AppManager.Instance.GetProfileItem(_config.IndexId);
                if (profile != null && profile.Subid == _config.SubIndexId)
                {
                    await Reload();
                }
            }

            if (_config.UiItem.EnableAutoAdjustMainLvColWidth)
            {
                await ProfilesViewModel.AdjustMainLvColWidth();
            }
        }
    }

    private async Task UpdateStatisticsHandler(ServerSpeedItem update)
    {
        if (!AppManager.Instance.ShowInTaskbar)
        {
            return;
        }
        AppEvents.DispatcherStatisticsRequested.Publish(update);
        await Task.CompletedTask;
    }

    #endregion Actions

    #region Servers && Groups

    private readonly SemaphoreSlim _refreshServersSemaphore = new(1, 1);

    private async Task RefreshServers()
    {
        await ProfilesViewModel.RefreshServersBiz();
        await StatusBarViewModel.RefreshServersBiz();

        // await Task.Delay(200);
    }

    private async Task RefreshServersDispatcherAsync()
    {
        //await Observable.Start(async () => await RefreshServers(), RxSchedulers.MainThreadScheduler);
        await _refreshServersSemaphore.WaitAsync();
        try
        {
            await Signal.FromAsync(async () =>
            {
                await RefreshServers();
                return RxVoid.Default;
            })
                .SubscribeOn(RxSchedulers.MainThreadScheduler)
                .ToTask();
        }
        finally
        {
            _refreshServersSemaphore.Release();
        }
    }

    private async Task RefreshSubscriptions()
    {
        //await Observable.Start(async () => await ProfilesViewModel.RefreshSubscriptions(), RxSchedulers.MainThreadScheduler);
        await Signal.FromAsync(async () =>
            {
                await ProfilesViewModel.RefreshSubscriptions();
                return RxVoid.Default;
            })
            .SubscribeOn(RxSchedulers.MainThreadScheduler)
            .ToTask();
    }

    #endregion Servers && Groups

    #region Add Servers

    public async Task AddServerAsync(EConfigType eConfigType)
    {
        ProfileItem item = new()
        {
            Subid = _config.SubIndexId,
            ConfigType = eConfigType,
            IsSub = false,
        };

        bool? ret = false;
        if (eConfigType is EConfigType.Custom or EConfigType.Outbound)
        {
            var addServer2ViewModel = new AddServer2ViewModel(item);
            ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(addServer2ViewModel);
        }
        else if (eConfigType.IsGroupType())
        {
            var addGroupServerViewModel = new AddGroupServerViewModel(item);
            ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(addGroupServerViewModel);
        }
        else
        {
            var addServerViewModel = new AddServerViewModel(item);
            ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(addServerViewModel);
        }
        if (ret == true)
        {
            await RefreshServersDispatcherAsync();
            if (item.IndexId == _config.IndexId)
            {
                await Reload();
            }
        }
    }

    public async Task AddServerViaClipboardAsync(string? clipboardData)
    {
        var stringData = clipboardData;
        if (clipboardData == null)
        {
            var result = await ReadTextFromClipboardInteraction.HandleSafe(RxVoid.Default);
            if (result.IsNullOrEmpty())
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
                return;
            }
            stringData = result;
        }
        var ret = await ConfigHandler.AddBatchServers(_config, stringData, _config.SubIndexId, false);
        if (ret > 0)
        {
            await RefreshSubscriptions();
            await RefreshServersDispatcherAsync();
            NoticeManager.Instance.Enqueue(string.Format(ResUI.SuccessfullyImportedServerViaClipboard, ret));
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    public async Task AddServerViaScanAsync()
    {
        var result = await ScanScreenInteraction.HandleSafe(RxVoid.Default);
        await ScanScreenResult(result);
    }

    public async Task ScanScreenResult(byte[]? bytes)
    {
        var result = QRCodeUtils.ParseBarcode(bytes);
        await AddScanResultAsync(result);
    }

    public async Task AddServerViaImageAsync()
    {
        var imageFileName = await BrowseImageFileInteraction.HandleSafe(RxVoid.Default);
        await ScanImageResult(imageFileName);
    }

    public async Task ScanImageResult(string fileName)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }

        var result = QRCodeUtils.ParseBarcode(fileName);
        await AddScanResultAsync(result);
    }

    private async Task AddScanResultAsync(string? result)
    {
        if (result.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.NoValidQRcodeFound);
        }
        else
        {
            var ret = await ConfigHandler.AddBatchServers(_config, result, _config.SubIndexId, false);
            if (ret > 0)
            {
                await RefreshSubscriptions();
                await RefreshServersDispatcherAsync();
                NoticeManager.Instance.Enqueue(ResUI.SuccessfullyImportedServerViaScan);
            }
            else
            {
                NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            }
        }
    }

    #endregion Add Servers

    #region Subscription

    private async Task SubSettingAsync()
    {
        var subSettingViewModel = new SubSettingViewModel();
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(subSettingViewModel) == true)
        {
            await RefreshSubscriptions();
        }
    }

    public async Task UpdateSubscriptionProcess(string subId, bool blProxy)
    {
        if (!await HasExistingSubscriptionConfiguration())
        {
            // Keep the established first-run onboarding chain unchanged.
            var initialSuccess = await Task.Run(
                async () => await SubscriptionHandler.UpdateProcess(_config, subId, blProxy, UpdateTaskHandler));
            if (initialSuccess)
            {
                AppEvents.SubscriptionAutoSpeedtestRequested.Publish();
            }
            return;
        }

        var success = await UpdateSubscriptionProcessCore(subId, blProxy, false, -1);
        if (!success)
        {
            return;
        }

        if (await ConnectionHandler.HasProxyInternetAccess())
        {
            _consecutiveNetworkFailures = 0;
            PublishHealthyNetworkStatus();
        }
        else
        {
            PublishNetworkStatus(
                ENetworkAvailabilityState.Repairing,
                "当前网络不可用，正在自动修复",
                "订阅和测速已完成，等待下一次网络检查");
        }
    }

    private async Task<bool> UpdateSubscriptionProcessCore(
        string subId,
        bool blProxy,
        bool recoveryMode,
        int currentDelay)
    {
        await _subscriptionWorkflowSemaphore.WaitAsync();
        try
        {
            var subscriptionIds = await GetSubscriptionIds(subId);
            if (subscriptionIds.Count == 0)
            {
                PublishNetworkStatus(
                    ENetworkAvailabilityState.NoAvailableNode,
                    "无可用节点，请联系客服",
                    "没有可更新的订阅");
                return false;
            }

            var activeBeforeUpdate = _config.IndexId;
            var keepActiveServer = !recoveryMode
                && _config.SystemProxyItem.SysProxyType == ESysProxyType.ForcedChange
                && await AppManager.Instance.GetProfileItem(activeBeforeUpdate) is not null;

            foreach (var id in subscriptionIds)
            {
                await SubscriptionSnapshotHandler.CaptureAsync(_config, id);
            }

            var success = await Task.Run(
                async () => await SubscriptionHandler.UpdateProcess(_config, subId, blProxy, UpdateTaskHandler));
            if (!success)
            {
                var restored = await RestoreSubscriptionSnapshots(subscriptionIds);
                if (restored)
                {
                    await RefreshServersDispatcherAsync();
                    await Reload();
                }
                return false;
            }

            var testResult = await ProfilesViewModel.TestAndSortSubscriptions(subscriptionIds);
            var restoredIds = new List<string>();
            foreach (var id in subscriptionIds.Where(id => !testResult.AvailableSubscriptionIds.Contains(id)))
            {
                if (await SubscriptionSnapshotHandler.RestoreAsync(_config, id))
                {
                    restoredIds.Add(id);
                }
            }
            if (restoredIds.Count > 0)
            {
                Logging.SaveLog($"New subscription nodes were unavailable; restored snapshots for {restoredIds.Count} subscription(s).");
                testResult = await ProfilesViewModel.TestAndSortSubscriptions(subscriptionIds);
            }

            foreach (var id in testResult.AvailableSubscriptionIds)
            {
                await SubscriptionSnapshotHandler.CaptureAsync(_config, id);
            }

            await RefreshSubscriptions();
            var currentProfile = await AppManager.Instance.GetProfileItem(_config.IndexId);
            var proxyConfigured = true;
            if (recoveryMode)
            {
                if (testResult.BestProfile is not null
                    && LatencySortHelper.ShouldSwitchToCandidate(
                        currentProfile is not null,
                        currentDelay,
                        testResult.BestProfile.Delay))
                {
                    proxyConfigured = await ActivateServer(testResult.BestProfile.IndexId);
                }
                else
                {
                    proxyConfigured = await StatusBarViewModel.SetListenerType(ESysProxyType.ForcedChange);
                }
            }
            else if (!keepActiveServer || currentProfile is null)
            {
                if (testResult.BestProfile is null)
                {
                    PublishNetworkStatus(
                        ENetworkAvailabilityState.NoAvailableNode,
                        "无可用节点，请联系客服",
                        "订阅中的所有节点均不可用");
                    return false;
                }
                proxyConfigured = await ActivateServer(testResult.BestProfile.IndexId);
            }

            return proxyConfigured
                && (testResult.BestProfile is not null
                    || await AppManager.Instance.GetProfileItem(_config.IndexId) is not null);
        }
        finally
        {
            _subscriptionWorkflowSemaphore.Release();
        }
    }

    private async Task<List<string>> GetSubscriptionIds(string subId)
    {
        var subscriptions = await AppManager.Instance.SubItems() ?? [];
        return subscriptions
            .Where(item => item.Enabled
                && item.Id.IsNotEmpty()
                && item.Url.IsNotEmpty()
                && (subId.IsNullOrEmpty() || item.Id == subId))
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<bool> HasExistingSubscriptionConfiguration()
    {
        var subscriptionIds = await GetSubscriptionIds("");
        if (subscriptionIds.Count == 0)
        {
            return false;
        }

        var profiles = await AppManager.Instance.ProfileItems(string.Empty);
        return NetworkRecoveryEligibility.CanRun(
            subscriptionIds.Count,
            profiles?.Count ?? 0);
    }

    private async Task<bool> RestoreSubscriptionSnapshots(IEnumerable<string> subscriptionIds)
    {
        var restored = false;
        foreach (var id in subscriptionIds)
        {
            restored |= await SubscriptionSnapshotHandler.RestoreAsync(_config, id);
        }
        return restored;
    }

    private async Task<bool> ActivateServer(string indexId)
    {
        if (indexId.IsNullOrEmpty() || await AppManager.Instance.GetProfileItem(indexId) is null)
        {
            return false;
        }

        var changed = _config.IndexId != indexId;
        if (changed)
        {
            await ConfigHandler.SetDefaultServerIndex(_config, indexId);
            await RefreshServersDispatcherAsync();
        }
        await Reload();
        if (!CoreManager.Instance.IsRunning)
        {
            Logging.SaveLog($"Network recovery could not start the selected server. Server={indexId}.");
            return false;
        }
        var proxyConfigured = await StatusBarViewModel.SetListenerType(ESysProxyType.ForcedChange);
        Logging.SaveLog($"Network recovery activated server. Server={indexId}, Changed={changed}.");
        return proxyConfigured;
    }

    private void StartNetworkHealthMonitor()
    {
        if (!_networkHealthMonitorEnabled)
        {
            return;
        }

        _networkHealthMonitorEnabled = true;
        PublishNetworkStatus(
            ENetworkAvailabilityState.Checking,
            "正在检查当前网络",
            "首次检查将在服务启动后自动进行");
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), _networkHealthCts.Token);
                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
                do
                {
                    await CheckNetworkHealth();
                }
                while (await timer.WaitForNextTickAsync(_networkHealthCts.Token));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Network health monitor failed", ex);
            }
        });
    }

    private async Task CheckNetworkHealth()
    {
        if (!_networkHealthMonitorEnabled
            || !await HasExistingSubscriptionConfiguration()
            || _networkRepairSemaphore.CurrentCount == 0
            || _subscriptionWorkflowSemaphore.CurrentCount == 0)
        {
            return;
        }

        if (!await ConnectionHandler.HasDirectInternetAccess())
        {
            _consecutiveNetworkFailures = 0;
            PublishNetworkStatus(
                ENetworkAvailabilityState.LocalNetworkUnavailable,
                "本机网络不可用，请检查 Wi-Fi 或网线");
            return;
        }

        var active = await AppManager.Instance.GetProfileItem(_config.IndexId);
        if (active is null)
        {
            var subscriptionIds = await GetSubscriptionIds("");
            if (subscriptionIds.Count > 0)
            {
                _consecutiveNetworkFailures++;
                PublishNetworkStatus(
                    ENetworkAvailabilityState.Repairing,
                    "当前网络不可用，正在自动修复",
                    $"尚未选择可用节点，连续检测 {_consecutiveNetworkFailures}/{AutomaticRepairFailureThreshold}");
                if (_consecutiveNetworkFailures >= AutomaticRepairFailureThreshold)
                {
                    await RunNetworkSetup(true);
                }
                return;
            }

            _consecutiveNetworkFailures = 0;
            PublishNetworkStatus(
                ENetworkAvailabilityState.NoAvailableNode,
                "无可用节点，请联系客服",
                "尚未添加可用订阅");
            return;
        }

        if (await ConnectionHandler.HasProxyInternetAccess())
        {
            _consecutiveNetworkFailures = 0;
            PublishHealthyNetworkStatus();
            return;
        }

        _consecutiveNetworkFailures++;
        Logging.SaveLog($"Proxy network health check failed. ConsecutiveFailures={_consecutiveNetworkFailures}.");
        if (_consecutiveNetworkFailures >= AutomaticRepairFailureThreshold)
        {
            await RunNetworkSetup(true);
        }
    }

    private async Task RunNetworkSetup(bool automatic)
    {
        var acquired = automatic
            ? await _networkRepairSemaphore.WaitAsync(0)
            : await WaitForNetworkRepairSemaphore();
        if (!acquired)
        {
            return;
        }

        try
        {
            PublishNetworkStatus(
                ENetworkAvailabilityState.Repairing,
                "当前网络不可用，正在自动修复",
                automatic ? "连续检测异常，正在选择可用节点" : "正在执行一键网络设置");

            if (!await ConnectionHandler.HasDirectInternetAccess())
            {
                _consecutiveNetworkFailures = 0;
                PublishNetworkStatus(
                    ENetworkAvailabilityState.LocalNetworkUnavailable,
                    "本机网络不可用，请检查 Wi-Fi 或网线");
                return;
            }

            var active = await AppManager.Instance.GetProfileItem(_config.IndexId);
            var currentDelay = active is null ? -1 : await ConnectionHandler.GetRealPingTimeInfo();
            if (currentDelay is > 0 and <= SlowNetworkDelay)
            {
                var proxyConfigured = await StatusBarViewModel.SetListenerType(ESysProxyType.ForcedChange);
                if (proxyConfigured && await ConnectionHandler.HasProxyInternetAccess())
                {
                    _consecutiveNetworkFailures = 0;
                    PublishHealthyNetworkStatus();
                    return;
                }
            }

            var repaired = false;
            if (automatic)
            {
                var existingIds = await GetSubscriptionIds("");
                var existingResult = await ProfilesViewModel.TestAndSortSubscriptions(existingIds);
                if (existingResult.BestProfile is not null)
                {
                    var proxyConfigured = await ActivateServer(existingResult.BestProfile.IndexId);
                    repaired = proxyConfigured && await ConnectionHandler.HasProxyInternetAccess();
                }
            }

            if (!repaired)
            {
                repaired = await UpdateSubscriptionProcessCore("", false, true, currentDelay);
                if (repaired)
                {
                    repaired = await ConnectionHandler.HasProxyInternetAccess();
                }
            }

            if (repaired)
            {
                _consecutiveNetworkFailures = 0;
                _lastNetworkRepairAt = DateTimeOffset.Now;
                PublishHealthyNetworkStatus();
            }
            else
            {
                PublishNetworkStatus(
                    ENetworkAvailabilityState.NoAvailableNode,
                    "无可用节点，请联系客服",
                    "订阅已更新，但没有节点能够访问目标网络");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Network setup failed", ex);
            PublishNetworkStatus(
                ENetworkAvailabilityState.NoAvailableNode,
                "无可用节点，请联系客服",
                "自动修复未能完成，请查看日志");
        }
        finally
        {
            _networkRepairSemaphore.Release();
        }
    }

    private async Task<bool> WaitForNetworkRepairSemaphore()
    {
        await _networkRepairSemaphore.WaitAsync();
        return true;
    }

    private void PublishHealthyNetworkStatus()
    {
        if (_lastNetworkRepairAt is { } repairedAt
            && DateTimeOffset.Now - repairedAt <= RepairedStatusDuration)
        {
            PublishNetworkStatus(
                ENetworkAvailabilityState.Repaired,
                "当前网络已自动修复",
                $"修复于 {repairedAt:HH:mm}");
            return;
        }

        PublishNetworkStatus(ENetworkAvailabilityState.Available, "当前网络可用");
    }

    private static void PublishNetworkStatus(
        ENetworkAvailabilityState state,
        string message,
        string detail = "")
    {
        AppEvents.NetworkAvailabilityChanged.Publish(new NetworkAvailabilityInfo(state, message, detail));
    }

    #endregion Subscription

    #region Setting

    private async Task OptionSettingAsync()
    {
        var settingViewModel = new OptionSettingViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(settingViewModel);
        if (ret == true)
        {
            MainGirdOrientation = _config.UiItem.MainGirdOrientation;
            RxSchedulers.MainThreadScheduler.Schedule(async () =>
            {
                await StatusBarViewModel.InboundDisplayStatus();
            });
            await Reload();
        }
    }

    private async Task RoutingSettingAsync()
    {
        var routingSettingViewModel = new RoutingSettingViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(routingSettingViewModel);
        if (ret == true)
        {
            await ConfigHandler.InitBuiltinRouting(_config);
            RxSchedulers.MainThreadScheduler.Schedule(async () =>
            {
                await StatusBarViewModel.RefreshRoutingsMenu();
            });
            await Reload();
        }
    }

    private async Task DNSSettingAsync()
    {
        var dnsSettingViewModel = new DNSSettingViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(dnsSettingViewModel);
        if (ret == true)
        {
            await Reload();
        }
    }

    private async Task FullConfigTemplateAsync()
    {
        var fullConfigTemplateViewModel = new FullConfigTemplateViewModel();
        var ret = await AppManager.Instance.WindowDialog.ShowDialogAsync(fullConfigTemplateViewModel);
        if (ret == true)
        {
            await Reload();
        }
    }

    private async Task ClearServerStatistics()
    {
        await StatisticsManager.Instance.ClearAllServerStatistics();
        await RefreshServersDispatcherAsync();
    }

    private async Task OpenTheFileLocation()
    {
        var path = Utils.StartupPath();
        if (Utils.IsWindows())
        {
            ProcUtils.ProcessStart(path);
        }
        else if (Utils.IsLinux())
        {
            ProcUtils.ProcessStart("xdg-open", path);
        }
        else if (Utils.IsMacOS())
        {
            ProcUtils.ProcessStart("open", path);
        }
        await Task.CompletedTask;
    }

    #endregion Setting

    #region core job

    private bool _hasNextReloadJob = false;
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);

    public async Task Reload()
    {
        //If there are unfinished reload job, marked with next job.
        if (!await _reloadSemaphore.WaitAsync(0))
        {
            _hasNextReloadJob = true;
            return;
        }

        if (DesignMode)
        {
            _reloadSemaphore.Release();
            return;
        }

        try
        {
            SetReloadEnabled(false);

            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                if (TabMainSelectedIndex < 0)
                {
                    TabMainSelectedIndex = 0;
                }
            });
            var profileItem = await ConfigHandler.GetDefaultServer(_config);
            if (profileItem == null)
            {
                NoticeManager.Instance.Enqueue(ResUI.CheckServerSettings);
                return;
            }
            var allResult = await CoreConfigContextBuilder.BuildAll(_config, profileItem);
            if (NoticeManager.Instance.NotifyValidatorResult(allResult.CombinedValidatorResult) && !allResult.Success)
            {
                return;
            }

            await Task.Run(async () =>
            {
                await LoadCore(allResult.MainResult.Context, allResult.PreSocksResult?.Context);
                if (_networkHealthMonitorEnabled && !CoreManager.Instance.IsRunning)
                {
                    throw new InvalidOperationException(ResUI.FailedToRunCore);
                }
                await SysProxyHandler.UpdateSysProxy(_config, false);
                await Task.Delay(1000);
            });
            RxSchedulers.MainThreadScheduler.Schedule(async () =>
            {
                var result = await StatusBarViewModel.TestServerAvailability();
                if (result == null || profileItem.IndexId.IsNullOrEmpty())
                {
                    return;
                }

                await ProfilesViewModel.SetSpeedTestResult(new()
                {
                    IndexId = profileItem.IndexId,
                    IpInfo = result.Ip,
                    Delay = result.Time > 0 ? result.Time.ToString() : null
                });
            });

            var showClashUI = AppManager.Instance.IsRunningCore(ECoreType.sing_box);
            if (showClashUI)
            {
                //await Observable.Start(async () =>
                //{
                //    await ClashProxiesViewModel.ProxiesReload();
                //}, RxSchedulers.MainThreadScheduler);
                await Signal.FromAsync(async () =>
                    {
                        await ClashProxiesViewModel.ProxiesReload();
                        return RxVoid.Default;
                    }).SubscribeOn(RxSchedulers.MainThreadScheduler)
                    .ToTask();
            }

            ReloadResult(showClashUI);
        }
        catch (Exception ex) when (_networkHealthMonitorEnabled)
        {
            Logging.SaveLog("Core reload failed; clearing the operating-system proxy before recovery.", ex);
            await SysProxyHandler.UpdateSysProxy(_config, true);
            PublishNetworkStatus(
                ENetworkAvailabilityState.Repairing,
                "当前网络不可用，正在自动修复",
                "代理核心启动失败，已撤销系统代理并尝试其他节点");
            _ = Task.Run(async () => await RunNetworkSetup(true));
        }
        finally
        {
            SetReloadEnabled(true);
            _reloadSemaphore.Release();
            //If there is a next reload job, execute it.
            if (_hasNextReloadJob)
            {
                _hasNextReloadJob = false;
                await Reload();
            }
        }
    }

    private void ReloadResult(bool showClashUI)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() =>
        {
            ShowClashUI = showClashUI;
            if (!showClashUI || TabMainSelectedIndex < 0)
            {
                TabMainSelectedIndex = 0;
            }
        });
    }

    private void SetReloadEnabled(bool enabled)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() => BlReloadEnabled = enabled);
    }

    private async Task LoadCore(CoreConfigContext? mainContext, CoreConfigContext? preContext)
    {
        await CoreManager.Instance.LoadCore(mainContext, preContext);
    }

    #endregion core job

    #region Presets

    public async Task ApplyRegionalPreset(EPresetType type)
    {
        await ConfigHandler.ApplyRegionalPreset(_config, type);
        await ConfigHandler.InitRouting(_config);
        RxSchedulers.MainThreadScheduler.Schedule(async () =>
        {
            await StatusBarViewModel.RefreshRoutingsMenu();
        });

        await ConfigHandler.SaveConfig(_config);
        await new UpdateService(_config, UpdateTaskHandler).UpdateGeoFileAll();
        await Reload();
    }

    #endregion Presets
}
