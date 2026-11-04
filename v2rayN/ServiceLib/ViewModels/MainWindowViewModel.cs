namespace ServiceLib.ViewModels;

public partial class MainWindowViewModel : MyReactiveObject
{
    public Interaction<RxVoid, string?> ReadTextFromClipboardInteraction { get; } = new();
    public Interaction<RxVoid, byte[]?> ScanScreenInteraction { get; } = new();
    public Interaction<RxVoid, string?> BrowseImageFileInteraction { get; } = new();
    public Interaction<bool?, RxVoid> ShowHideWindowInteraction { get; } = new();

    public bool DesignMode { get; set; }

    public ProfilesViewModel ProfilesViewModel { get; }
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
    private readonly NetworkWorkflowGate _networkWorkflow = new(AppEvents.NetworkSetupRunningChanged.Publish);
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
        ProfilesViewModel = new ProfilesViewModel(EnsureSelectedServerActive);
        _config = AppManager.Instance.Config;
        _config.UiItem ??= new();
        ConfigHandler.ApplyUiLayoutMigration(_config.UiItem);
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
            && (await NetworkRecoveryEligibility.ReadAsync()).CanMonitor;
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

    public Task UpdateSubscriptionProcess(string subId, bool blProxy) =>
        _networkWorkflow.RunAsync(() => UpdateSubscription(subId, blProxy), wait: true);

    private async Task UpdateSubscription(string subId, bool blProxy)
    {
        if (!(await NetworkRecoveryEligibility.ReadAsync()).CanMonitor)
        {
            // Keep the established first-run onboarding chain unchanged.
            var initialSuccess = await Task.Run(
                async () => await SubscriptionHandler.UpdateProcess(_config, subId, blProxy, UpdateTaskHandler));
            if (initialSuccess)
            {
                var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                AppEvents.SubscriptionAutoSpeedtestRequested.Publish(completed);
                using var onExit = _networkHealthCts.Token.Register(() => completed.TrySetResult(false));
                if (!await completed.Task && !_networkHealthCts.IsCancellationRequested)
                {
                    PublishNetworkStatus(ENetworkAvailabilityState.SetupFailed, "订阅已下载，节点测速或代理设置未完成");
                }
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
                ENetworkAvailabilityState.SetupFailed,
                "订阅已更新，代理联网验证失败",
                "请检查节点，或重试一键网络设置");
        }
    }

    private async Task<bool> UpdateSubscriptionProcessCore(
        string subId,
        bool blProxy,
        bool recoveryMode,
        int currentDelay,
        AutomaticRecoveryInputs? automaticInputs = null)
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

            var tickets = await SubscriptionWrites.Capture(subscriptionIds);
            var activeBeforeUpdate = _config.IndexId;
            var keepActiveServer = !recoveryMode
                && TunTakeoverPolicy.IsEffectiveTakeoverActive(_config)
                && await AppManager.Instance.GetProfileItem(activeBeforeUpdate) is not null;

            foreach (var id in subscriptionIds)
            {
                await tickets[id].IfCurrent(() => SubscriptionSnapshotHandler.CaptureAsync(_config, id));
            }

            await CheckRecoveryInputs(automaticInputs);
            var success = await Task.Run(
                async () => await SubscriptionHandler.UpdateProcess(_config, subId, blProxy, UpdateTaskHandler, tickets));
            if (!success)
            {
                var restored = await RestoreSubscriptionSnapshots(tickets);
                if (restored)
                {
                    await RefreshServersDispatcherAsync();
                    await Reload();
                }
                return false;
            }

            subscriptionIds = await SubscriptionWrites.CurrentIds(tickets);
            if (subscriptionIds.Count == 0)
            {
                return false;
            }
            var testResult = await ProfilesViewModel.TestAndSortSubscriptions(subscriptionIds);
            var restoredIds = new List<string>();
            foreach (var id in subscriptionIds.Where(id => !testResult.AvailableSubscriptionIds.Contains(id)))
            {
                if (await tickets[id].Restore(() => SubscriptionSnapshotHandler.RestoreAsync(_config, id)))
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
                await tickets[id].IfCurrent(() => SubscriptionSnapshotHandler.CaptureAsync(_config, id));
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
                    proxyConfigured = await ActivateVerifiedServer(testResult.BestProfile.IndexId);
                }
                else
                {
                    var run = _runningCore;
                    proxyConfigured = await VerifyRunningProfile(run) && await ConfigureRunningProxy(run);
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
            .Where(item => item.Enabled && SubscriptionHandler.IsValidSubscription(item, subId))
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<bool> RestoreSubscriptionSnapshots(Dictionary<string, SubscriptionWrites.Ticket> tickets)
    {
        var restored = false;
        foreach (var (id, ticket) in tickets)
        {
            restored |= await ticket.Restore(() => SubscriptionSnapshotHandler.RestoreAsync(_config, id));
        }
        return restored;
    }

    private async Task<RunningCore?> StartServer(string indexId)
    {
        if (indexId.IsNullOrEmpty() || await AppManager.Instance.GetProfileItem(indexId) is null)
        {
            return null;
        }
        if (_config.IndexId != indexId)
        {
            await ConfigHandler.SetDefaultServerIndex(_config, indexId);
            await RefreshServersDispatcherAsync();
        }
        return await ReloadCore(indexId);
    }

    private Task<bool> EnsureSelectedServerActive(string id)
    {
        var run = _runningCore;
        return run?.ProfileId == id && IsCurrent(run) ? ConfigureRunningProxy(run) : ActivateServer(id);
    }

    private async Task<bool> ActivateServer(string indexId)
    {
        return await ActivateServer(indexId, StartServer, ConfigureRunningProxy);
    }

    internal static async Task<bool> ActivateServer(string id, Func<string, Task<RunningCore?>> start, Func<RunningCore?, Task<bool>> configure)
    {
        var run = await start(id);
        return run is not null && await configure(run);
    }

    private async Task<bool> ActivateVerifiedServer(string indexId)
    {
        var run = await StartServer(indexId);
        return await VerifyRunningProfile(run) && await ConfigureRunningProxy(run);
    }

    private async Task<bool> ConfigureRunningProxy(RunningCore? run)
    {
        await _reloadSemaphore.WaitAsync();
        try
        {
            return IsCurrent(run)
                && await StatusBarViewModel.SetListenerType(TunTakeoverPolicy.GetDesiredSysProxyForWorkflow(_config))
                && IsCurrent(run);
        }
        finally
        {
            _reloadSemaphore.Release();
        }
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
            "首次检查将在服务启动后自动进行", AppEvents.ProfileRevision);
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
        var revision = AppEvents.ProfileRevision;
        if (!_networkHealthMonitorEnabled
            || !(await NetworkRecoveryEligibility.ReadAsync()).CanMonitor
            || _networkWorkflow.IsBusy
            || _subscriptionWorkflowSemaphore.CurrentCount == 0)
        {
            return;
        }

        var direct = await ConnectionHandler.HasDirectInternetAccess();
        if (revision != AppEvents.ProfileRevision)
        {
            return;
        }
        if (!direct)
        {
            _consecutiveNetworkFailures = 0;
            PublishNetworkStatus(ENetworkAvailabilityState.LocalNetworkUnavailable,
                "本机网络不可用，请检查 Wi-Fi 或网线", monitorRevision: revision);
            return;
        }

        var active = await AppManager.Instance.GetProfileItem(_config.IndexId);
        var reachable = active is not null && await ConnectionHandler.HasProxyInternetAccess();
        if (revision != AppEvents.ProfileRevision || _networkWorkflow.IsBusy)
        {
            return;
        }
        if (reachable)
        {
            _consecutiveNetworkFailures = 0;
            PublishHealthyNetworkStatus(revision);
            return;
        }

        _consecutiveNetworkFailures++;
        Logging.SaveLog($"Proxy network health check failed. ConsecutiveFailures={_consecutiveNetworkFailures}.");
        if (_consecutiveNetworkFailures >= AutomaticRepairFailureThreshold)
        {
            await RunNetworkSetup(true, revision);
        }
        else
        {
            PublishNetworkStatus(ENetworkAvailabilityState.SetupFailed, "代理暂未连通，等待复查",
                $"连续检测异常 {_consecutiveNetworkFailures}/{AutomaticRepairFailureThreshold}", revision);
        }
    }

    private Task RunNetworkSetup(bool automatic, long? monitorRevision = null) =>
        _networkWorkflow.RunAsync(() => SetUpNetwork(automatic, monitorRevision), wait: false);

    private sealed class RecoveryInputsChangedException : Exception { }

    private static async Task CheckRecoveryInputs(AutomaticRecoveryInputs? inputs)
    {
        if (inputs is not null && !inputs.Matches(await AutomaticRecoveryInputs.ReadAsync()))
        {
            throw new RecoveryInputsChangedException();
        }
    }

    private async Task SetUpNetwork(bool automatic, long? monitorRevision)
    {
        try
        {
            var configuration = await NetworkRecoveryEligibility.ReadAsync();
            if (automatic && (!configuration.CanMonitor ||
                (monitorRevision.HasValue && monitorRevision != AppEvents.ProfileRevision)))
            {
                return;
            }
            if (!configuration.CanSetUp)
            {
                _consecutiveNetworkFailures = 0;
                PublishNetworkStatus(ENetworkAvailabilityState.Idle, configuration.IdleMessage);
                return;
            }

            var inputs = automatic ? await AutomaticRecoveryInputs.ReadAsync() : null;
            if (monitorRevision.HasValue && monitorRevision != AppEvents.ProfileRevision)
            {
                return;
            }

            PublishNetworkStatus(
                ENetworkAvailabilityState.Repairing,
                "当前网络不可用，正在自动修复",
                automatic ? "连续检测异常，正在选择可用节点" : "正在执行一键网络设置");

            var direct = await ConnectionHandler.HasDirectInternetAccess();
            await CheckRecoveryInputs(inputs);
            if (!direct)
            {
                _consecutiveNetworkFailures = 0;
                PublishNetworkStatus(
                    ENetworkAvailabilityState.LocalNetworkUnavailable,
                    "本机网络不可用，请检查 Wi-Fi 或网线");
                return;
            }

            var active = await AppManager.Instance.GetProfileItem(_config.IndexId);
            if (!configuration.HasSubscription)
            {
                await SetUpLocalNodes(active);
                return;
            }

            var currentDelay = active is null ? -1 : await ConnectionHandler.GetRealPingTimeInfo();
            await CheckRecoveryInputs(inputs);
            if (currentDelay is > 0 and <= SlowNetworkDelay)
            {
                var run = _runningCore;
                if (run?.ProfileId == active?.IndexId && await VerifyRunningProfile(run) && await ConfigureRunningProxy(run))
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
                await CheckRecoveryInputs(inputs);
                var existingResult = await ProfilesViewModel.TestAndSortSubscriptions(existingIds);
                await CheckRecoveryInputs(inputs);
                if (existingResult.BestProfile is not null)
                {
                    repaired = await ActivateVerifiedServer(existingResult.BestProfile.IndexId);
                }
            }

            if (!repaired)
            {
                await CheckRecoveryInputs(inputs);
                repaired = await UpdateSubscriptionProcessCore("", false, true, currentDelay, inputs);
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
                    "未能通过订阅恢复代理连接，请检查订阅和节点");
            }
        }
        catch (RecoveryInputsChangedException)
        {
            _consecutiveNetworkFailures = 0;
            var current = await NetworkRecoveryEligibility.ReadAsync();
            PublishNetworkStatus(ENetworkAvailabilityState.Idle, current.IdleMessage);
        }
        catch (OperationCanceledException) when (_networkHealthCts.IsCancellationRequested)
        {
            // The application is exiting.
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Network setup failed", ex);
            PublishNetworkStatus(
                ENetworkAvailabilityState.SetupFailed,
                "网络设置未完成",
                "自动修复未能完成，请查看日志");
        }
    }

    private async Task SetUpLocalNodes(ProfileItem? active)
    {
        var run = _runningCore;
        if (active is not null && run?.ProfileId == active.IndexId && await VerifyRunningProfile(run))
        {
            if (await EnableLocalNodeProxy(run))
            {
                _consecutiveNetworkFailures = 0;
                PublishHealthyNetworkStatus();
            }
            return;
        }

        var candidates = await ProfilesViewModel.TestLocalAlternatives(active?.IndexId);
        bool repaired;
        try
        {
            repaired = await TryLocalNodes(candidates, active?.IndexId,
                async id =>
                {
                    run = await StartServer(id);
                    return await VerifyRunningProfile(run);
                }, _networkHealthCts.Token);
        }
        catch (OperationCanceledException) when (_networkHealthCts.IsCancellationRequested)
        {
            if (active is not null && _config.IndexId != active.IndexId)
            {
                await ConfigHandler.SetDefaultServerIndex(_config, active.IndexId);
            }
            throw;
        }
        if (!repaired)
        {
            PublishNetworkStatus(
                ENetworkAvailabilityState.LocalNodesUnavailable,
                "本地节点未能连通",
                "请检查节点配置，或手动选择其他节点后重试");
            return;
        }
        if (!await EnableLocalNodeProxy(run))
        {
            return;
        }
        _consecutiveNetworkFailures = 0;
        _lastNetworkRepairAt = DateTimeOffset.Now;
        PublishHealthyNetworkStatus();
    }

    private async Task<bool> EnableLocalNodeProxy(RunningCore? run)
    {
        if (await ConfigureRunningProxy(run))
        {
            return true;
        }
        PublishNetworkStatus(ENetworkAvailabilityState.SetupFailed, "节点已连通，但代理设置未完成");
        return false;
    }

    internal static async Task<bool> TryLocalNodes(
        IEnumerable<string> alternatives, string? originalId,
        Func<string, Task<bool>> startAndVerify, CancellationToken cancellationToken = default)
    {
        // Retry the original core last, leaving the original node selected if all alternatives fail.
        var candidates = originalId is null ? alternatives : alternatives.Append(originalId);
        foreach (var id in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var connected = await startAndVerify(id);
                cancellationToken.ThrowIfCancellationRequested();
                if (connected)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Logging.SaveLog($"Local node recovery failed for server {id}.", ex);
            }
        }
        return false;
    }

    private void PublishHealthyNetworkStatus(long? monitorRevision = null)
    {
        if (_lastNetworkRepairAt is { } repairedAt
            && DateTimeOffset.Now - repairedAt <= RepairedStatusDuration)
        {
            PublishNetworkStatus(
                ENetworkAvailabilityState.Repaired,
                "当前网络已自动修复",
                $"修复于 {repairedAt:HH:mm}", monitorRevision);
            return;
        }

        PublishNetworkStatus(ENetworkAvailabilityState.Available, "当前网络可用", monitorRevision: monitorRevision);
    }

    private static void PublishNetworkStatus(
        ENetworkAvailabilityState state,
        string message,
        string detail = "", long? monitorRevision = null)
    {
        AppEvents.NetworkAvailabilityChanged.Publish(new NetworkAvailabilityInfo(state, message, detail, monitorRevision));
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
            await StatusBarViewModel.SyncSysProxyAfterTunSettingChangedAsync();
            AppEvents.ProfilesRefreshRequested.Publish();
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

    // Each successful launch gets a distinct identity, even when it uses the same profile.
    internal sealed class RunningCore(string profileId)
    {
        public string ProfileId { get; } = profileId;
    }

    private volatile RunningCore? _runningCore;

    private bool IsCurrent(RunningCore? run) => run is not null && ReferenceEquals(run, _runningCore)
        && run.ProfileId == _config.IndexId && CoreManager.Instance.IsRunning;

    private Task<bool> VerifyRunningProfile(RunningCore? run) =>
        VerifyRunningProfile(run, IsCurrent, ConnectionHandler.HasProxyInternetAccess);

    internal static async Task<bool> VerifyRunningProfile(RunningCore? run, Func<RunningCore?, bool> isRunning, Func<Task<bool>> probe) =>
        run is not null && isRunning(run) && await probe() && isRunning(run);

    public async Task Reload() => await ReloadCore();

    private async Task<RunningCore?> ReloadCore(string? requiredId = null)
    {
        // Recovery must await its own reload. Ordinary UI reloads can coalesce.
        if (requiredId is not null)
        {
            await _reloadSemaphore.WaitAsync();
        }
        else if (!await _reloadSemaphore.WaitAsync(0))
        {
            _hasNextReloadJob = true;
            return null;
        }

        if (DesignMode)
        {
            _reloadSemaphore.Release();
            return null;
        }

        bool launchAttempted = false;
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
            if (profileItem == null || (requiredId is not null && profileItem.IndexId != requiredId))
            {
                NoticeManager.Instance.Enqueue(ResUI.CheckServerSettings);
                return null;
            }
            var allResult = await CoreConfigContextBuilder.BuildAll(_config, profileItem);
            if (NoticeManager.Instance.NotifyValidatorResult(allResult.CombinedValidatorResult) && !allResult.Success)
            {
                return null;
            }

            await Task.Run(async () =>
            {
                launchAttempted = true;
                _runningCore = null;
                var loaded = await CoreManager.Instance.LoadCore(allResult.MainResult.Context, allResult.PreSocksResult?.Context);
                if (!loaded)
                {
                    return;
                }
                _runningCore = new RunningCore(profileItem.IndexId);
                if (requiredId is null)
                {
                    await SysProxyHandler.UpdateSysProxy(_config, false);
                }
                await Task.Delay(1000);
            });
            if (_runningCore is null)
            {
                if (requiredId is null && _networkHealthMonitorEnabled)
                {
                    _ = Task.Run(async () => await RunNetworkSetup(true));
                }
                return null;
            }
            if (requiredId is not null && !IsCurrent(_runningCore))
            {
                return null;
            }
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
            return _runningCore;
        }
        catch (Exception ex) when (_networkHealthMonitorEnabled)
        {
            Logging.SaveLog("Core reload failed; checking the local listener before recovery.", ex);
            PublishNetworkStatus(
                ENetworkAvailabilityState.SetupFailed,
                "代理核心启动失败",
                "请检查核心日志");
            if (requiredId is null)
            {
                _ = Task.Run(async () => await RunNetworkSetup(true));
            }
            return null;
        }
        finally
        {
            try
            {
                if (launchAttempted && _runningCore is null)
                {
                    await ClearUnavailableProxy();
                }
            }
            finally
            {
                SetReloadEnabled(true);
                _reloadSemaphore.Release();
            }
            //If there is a next reload job, execute it.
            if (_hasNextReloadJob)
            {
                _hasNextReloadJob = false;
                await Reload();
            }
        }
    }

    private async Task ClearUnavailableProxy()
    {
        if (_config.SystemProxyItem.SysProxyType is not (ESysProxyType.ForcedChange or ESysProxyType.Pac))
        {
            return;
        }
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        if (!await ClearProxyIfSocksUnavailable(port, CoreManager.Instance.IsRunning, () => SysProxyHandler.UpdateSysProxy(_config, true)))
        {
            Logging.SaveLog("Unable to clear the system proxy after the core listener stopped.");
            NoticeManager.Instance.Enqueue("本地 SOCKS 代理不可用，清除系统代理失败，请检查系统代理设置");
        }
    }

    internal static async Task<bool> ClearProxyIfSocksUnavailable(int port, bool coreRunning, Func<Task<bool>> clear) =>
        (coreRunning && await ConnectionHandler.HasLocalSocksProxy(port)) || await clear();

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
