namespace ServiceLib.ViewModels;

public partial class ProfilesViewModel : MyReactiveObject
{
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<ProfileItem, bool> SaveFileDialogInteraction { get; } = new();
    public Interaction<string, RxVoid> SetClipboardDataInteraction { get; } = new();
    public Interaction<RxVoid, RxVoid> ProfilesFocusInteraction { get; } = new();
    public Interaction<string, RxVoid> ShareServerInteraction { get; } = new();
    public Interaction<RxVoid, RxVoid> DispatcherRefreshServersBizInteraction { get; } = new();
    public Interaction<RxVoid, RxVoid> AdjustMainLvColWidthInteraction { get; } = new();

    public EventChannel<RxVoid> ReloadRequested { get; } = new();
    public EventChannel<RxVoid> RefreshServersRequested { get; } = new();

    #region private prop

    private string _serverFilter = string.Empty;
    private readonly Dictionary<string, bool> _dicHeaderSort = new();
    private readonly Dictionary<string, (string SubId, int Delay)> _allNodeStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _subLastSpeedtestAt = new(StringComparer.Ordinal);
    private bool _syncingSubSelection;
    private int _cardRefreshVersion;
    private string _activeServerSubId = string.Empty;
    private bool _hasActiveServer;
    private SpeedtestService? _speedtestService;
    private CloudflareSpeedTestService? _cloudflareSpeedTestService;
    private string? _pendingSelectIndexId;
    private const string CloudflareBestNodeRemark = "最优节点";
    private const int CloudflareBestNodeLimit = 100;
    private const int CloudflareBestNodeMaxDelay = 500;

    #endregion private prop

    #region ObservableCollection

    public BulkObservableCollection<ProfileItemModel> ProfileItems { get; } = [];

    public BulkObservableCollection<SubItem> SubItems { get; } = [];

    public BulkObservableCollection<SubscriptionCardItem> SubscriptionCards { get; } = [];

    [Reactive]
    public partial ProfileItemModel SelectedProfile { get; set; }

    public IList<ProfileItemModel> SelectedProfiles { get; set; }

    [Reactive]
    public partial SubItem SelectedSub { get; set; }

    [Reactive]
    public partial SubscriptionCardItem? SelectedSubscriptionCard { get; set; }

    [Reactive]
    public partial SubItem SelectedMoveToGroup { get; set; }

    [Reactive]
    public partial string ServerFilter { get; set; }

    [Reactive]
    public partial bool CanShowCloudflareBestNodeMenu { get; set; }

    [Reactive]
    public partial bool IsCloudflareOptimizing { get; set; }

    [Reactive]
    public partial string CloudflareBestNodeMenuHeader { get; set; } = "以此节点自动生成最优节点";

    [Reactive]
    public partial string SelectedSubscriptionTraffic { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasSelectedSubscriptionTraffic { get; set; }

    #endregion ObservableCollection

    #region Menu

    //servers delete
    public ReactiveCommand<RxVoid, RxVoid> EditServerCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> RemoveServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RemoveDuplicateServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CopyServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SetDefaultServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> ShareServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> GenerateCloudflareBestNodesCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> GenGroupAllServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> GenGroupRegionServerCmd { get; }

    //servers move
    public ReactiveCommand<RxVoid, RxVoid> MoveTopCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> MoveUpCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveDownCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> MoveBottomCmd { get; }
    public ReactiveCommand<SubItem, RxVoid> MoveToGroupCmd { get; }

    //servers ping
    public ReactiveCommand<RxVoid, RxVoid> MixedTestServerCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> TcpingServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RealPingServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> UdpTestServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SpeedServerCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SortServerResultCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RemoveInvalidServerResultCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> FastRealPingCmd { get; }

    //servers export
    public ReactiveCommand<RxVoid, RxVoid> Export2ClientConfigCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> Export2ClientConfigClipboardCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> Export2ShareUrlCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> Export2ShareUrlBase64Cmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> Export2InnerUriCmd { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddSubCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> EditSubCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> DeleteSubCmd { get; }
    public ReactiveCommand<SubscriptionCardItem, RxVoid> UpdateSubscriptionCardCmd { get; }
    public ReactiveCommand<string, RxVoid> OpenSubscriptionUrlCmd { get; }

    #endregion Menu

    #region Init

    private readonly Func<string, Task<bool>>? _activateServer;

    public ProfilesViewModel() : this(null) { }

    internal ProfilesViewModel(Func<string, Task<bool>>? activateServer)
    {
        _activateServer = activateServer;
        _config = AppManager.Instance.Config;

        #region WhenAnyValue && ReactiveCommand

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedProfile,
           selectedSource => selectedSource != null && !selectedSource.IndexId.IsNullOrEmpty());
        var canGenerateCloudflareBestNodes = this.WhenAnyValue(
            x => x.SelectedProfile,
            x => x.IsCloudflareOptimizing,
            (selectedSource, optimizing) => selectedSource != null
                && !selectedSource.IndexId.IsNullOrEmpty()
                && !optimizing);

        this.WhenAnyValue(x => x.SelectedSubscriptionCard)
            .Subscribe(card =>
            {
                if (_syncingSubSelection || card is null)
                {
                    return;
                }

                _syncingSubSelection = true;
                try
                {
                    var targetSub = SubItems.FirstOrDefault(s => string.Equals(s.Id ?? string.Empty, card.Id, StringComparison.Ordinal))
                        ?? card.SubItem;
                    if (!ReferenceEquals(SelectedSub, targetSub))
                    {
                        SelectedSub = targetSub;
                    }
                }
                finally
                {
                    _syncingSubSelection = false;
                }
            });
        this.WhenAnyValue(x => x.SelectedSub)
            .Subscribe(sub =>
            {
                if (_syncingSubSelection || sub is null || sub.Remarks.IsNullOrEmpty())
                {
                    return;
                }

                _syncingSubSelection = true;
                try
                {
                    var targetId = sub.Id ?? string.Empty;
                    var targetCard = SubscriptionCards.FirstOrDefault(c => string.Equals(c.Id, targetId, StringComparison.Ordinal));
                    if (targetCard is not null && !ReferenceEquals(SelectedSubscriptionCard, targetCard))
                    {
                        SelectedSubscriptionCard = targetCard;
                    }
                }
                finally
                {
                    _syncingSubSelection = false;
                }
            });
        this.WhenAnyValue(x => x.SelectedSub)
            .Where(y => y != null && !y.Remarks.IsNullOrEmpty() && _config.SubIndexId != y.Id)
            .SubscribeAsync(async _ => await SubSelectedChangedAsync());
        this.WhenAnyValue(x => x.SelectedMoveToGroup)
            .Where(y => y != null && !y.Remarks.IsNullOrEmpty())
            .SubscribeAsync(async _ => await MoveToGroup());
        this.WhenAnyValue(
            x => x.SelectedProfile,
            selectedSource => selectedSource != null && !selectedSource.IndexId.IsNullOrEmpty())
                .Subscribe(v => CanShowCloudflareBestNodeMenu = v);
        this.WhenAnyValue(
            x => x.IsCloudflareOptimizing,
            optimizing => optimizing ? "优选中..." : "以此节点自动生成最优节点")
                .Subscribe(v => CloudflareBestNodeMenuHeader = v);

        this.WhenAnyValue(x => x.ServerFilter)
            .Where(y => y != null && _serverFilter != y)
            .SubscribeAsync(async _ => await ServerFilterChanged());

        //servers delete
        EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditServerAsync();
        }, canEditRemove);
        RemoveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveServerAsync();
        }, canEditRemove);
        RemoveDuplicateServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveDuplicateServer();
        });
        CopyServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyServer();
        }, canEditRemove);
        SetDefaultServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetDefaultServer();
        }, canEditRemove);
        ShareServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareServerAsync();
        }, canEditRemove);
        GenerateCloudflareBestNodesCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenerateCloudflareBestNodes();
        }, canGenerateCloudflareBestNodes);
        GenGroupAllServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupAllServer();
        }, canEditRemove);
        GenGroupRegionServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupRegionServer();
        }, canEditRemove);

        //servers move
        MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Top);
        }, canEditRemove);
        MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Up);
        }, canEditRemove);
        MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Down);
        }, canEditRemove);
        MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Bottom);
        }, canEditRemove);
        MoveToGroupCmd = ReactiveCommand.CreateFromTask<SubItem>(async sub =>
        {
            SelectedMoveToGroup = sub;
        });

        //servers ping
        FastRealPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Realping, testAll: true);
        });
        MixedTestServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Mixedtest);
        });
        TcpingServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Tcping);
        }, canEditRemove);
        RealPingServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Realping);
        }, canEditRemove);
        UdpTestServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.UdpTest);
        }, canEditRemove);
        SpeedServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Speedtest);
        }, canEditRemove);
        SortServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SortServer(nameof(EServerColName.DelayVal));
        });
        RemoveInvalidServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveInvalidServerResult();
        });
        //servers export
        Export2ClientConfigCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ClientConfigAsync(false);
        }, canEditRemove);
        Export2ClientConfigClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ClientConfigAsync(true);
        }, canEditRemove);
        Export2ShareUrlCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ShareUrlAsync(false);
        }, canEditRemove);
        Export2ShareUrlBase64Cmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ShareUrlAsync(true);
        }, canEditRemove);
        Export2InnerUriCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2InnerUrlAsync();
        }, canEditRemove);

        //Subscription
        AddSubCmd = ReactiveCommand.CreateFromTask(AddSubscription);
        EditSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        });
        DeleteSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DeleteSubAsync();
        });
        UpdateSubscriptionCardCmd = ReactiveCommand.Create<SubscriptionCardItem>(card =>
        {
            if (card is null || !card.CanUpdateSubscription)
            {
                return;
            }

            if (card.IsAllGroup)
            {
                AppEvents.SubscriptionsUpdateRequested.Publish(false);
                return;
            }

            if (card.Id.IsNotEmpty())
            {
                AppEvents.CurrentSubscriptionUpdateRequested.Publish(card.Id);
            }
        });
        OpenSubscriptionUrlCmd = ReactiveCommand.Create<string>(url =>
        {
            var safeUrl = SubscriptionCardItem.NormalizeExternalUrl(url);
            if (safeUrl.IsNotEmpty())
            {
                ProcUtils.ProcessStart(safeUrl);
            }
        });

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        StatusBarViewModel.Instance
            .WhenAnyValue(x => x.SelectedServer)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RefreshActiveConfigBadgesFromCache());

        AppEvents.ProfilesRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshServersBiz());

        AppEvents.SubscriptionsRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshSubscriptions());

        AppEvents.SubscriptionAutoSpeedtestRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async completion =>
            {
                try
                {
                    var success = await AutoRealPingAndSort();
                    completion?.TrySetResult(success);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("Automatic subscription speed test failed", ex);
                    completion?.TrySetResult(false);
                }
            });
        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeAsync(async result => await UpdateStatistics(result));

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        SelectedProfile = new();
        SelectedSub = new();
        SelectedMoveToGroup = new();

        await RefreshSubscriptions();
        //await RefreshServers();
    }

    #endregion Init

    #region Actions

    private void Reload()
    {
        ReloadRequested.Publish();
    }

    public async Task SetSpeedTestResult(SpeedTestResult result)
    {
        if (result.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.SendMessageEx(result.Delay);
            NoticeManager.Instance.Enqueue(result.Delay);
            return;
        }

        var item = ProfileItems.FirstOrDefault(it => it.IndexId == result.IndexId);
        if (result.Delay.IsNotEmpty())
        {
            var delayVal = result.Delay.ToInt();
            UpdateCardNodeDelayInPlace(result.IndexId, item?.Subid, delayVal);
        }

        if (item == null)
        {
            return;
        }

        if (result.Delay.IsNotEmpty())
        {
            item.Delay = result.Delay.ToInt();
            item.DelayVal = result.Delay ?? string.Empty;
        }
        if (result.Speed.IsNotEmpty())
        {
            item.SpeedVal = result.Speed ?? string.Empty;
        }
        if (result.IpInfo.IsNotEmpty())
        {
            item.IpInfo = result.IpInfo ?? string.Empty;
        }
        await Task.CompletedTask;
    }

    public async Task UpdateStatistics(ServerSpeedItem update)
    {
        if (!_config.GuiItem.EnableStatistics
            || (update.ProxyUp + update.ProxyDown) <= 0
            || DateTime.Now.Second % 3 != 0)
        {
            return;
        }

        try
        {
            var item = ProfileItems.FirstOrDefault(it => it.IndexId == update.IndexId);
            if (item != null)
            {
                item.TodayDown = Utils.HumanFy(update.TodayDown);
                item.TodayUp = Utils.HumanFy(update.TodayUp);
                item.TotalDown = Utils.HumanFy(update.TotalDown);
                item.TotalUp = Utils.HumanFy(update.TotalUp);
            }
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    #endregion Actions

    #region Servers && Groups

    private async Task SubSelectedChangedAsync()
    {
        UpdateSelectedSubscriptionTraffic(SelectedSub);
        _config.SubIndexId = SelectedSub?.Id;
        AppEvents.SubscriptionSelectionChanged.Publish(_config.SubIndexId ?? string.Empty);

        await RefreshServers();

        await ProfilesFocusInteraction.HandleSafe(RxVoid.Default);
    }

    private async Task ServerFilterChanged()
    {
        _serverFilter = ServerFilter;
        if (_serverFilter.IsNullOrEmpty())
        {
            await RefreshServers();
        }
    }

    public async Task RefreshServers()
    {
        RefreshServersRequested.Publish();

        // await Task.Delay(200);

        await Task.CompletedTask;
    }

    public async Task RefreshServersBiz()
    {
        var lstModel = await GetProfileItemsEx(_config.SubIndexId, _serverFilter);

        ProfileItems.ReplaceRange(lstModel ?? []);
        if (lstModel?.Count > 0)
        {
            ProfileItemModel? selected = null;
            if (!_pendingSelectIndexId.IsNullOrEmpty())
            {
                selected = lstModel.FirstOrDefault(t => t.IndexId == _pendingSelectIndexId);
                _pendingSelectIndexId = null;
            }
            selected ??= lstModel.FirstOrDefault(t => t.IndexId == _config.IndexId);
            SelectedProfile = selected ?? lstModel.First();
        }

        AppEvents.NotifyProfilesRefreshed();
        await RefreshSubscriptionCardsAsync();
        await DispatcherRefreshServersBizInteraction.HandleSafe(RxVoid.Default);
    }

    public async Task RefreshSubscriptions()
    {
        var subItems = await AppManager.Instance.SubItems() ?? [];
        subItems.Insert(0, new SubItem { Remarks = ResUI.AllGroupServers });

        SubItems.ReplaceRange(subItems);

        SelectedSub = (_config.SubIndexId.IsNotEmpty()
                        ? subItems.FirstOrDefault(t => t.Id == _config.SubIndexId)
                        : null) ?? subItems.FirstOrDefault();
        UpdateSelectedSubscriptionTraffic(SelectedSub);
        await RefreshSubscriptionCardsAsync();
    }

    private async Task RefreshSubscriptionCardsAsync()
    {
        var refreshVersion = ++_cardRefreshVersion;
        var allProfiles = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
        var profileExs = await ProfileExManager.Instance.GetProfileExs();
        if (refreshVersion != _cardRefreshVersion)
        {
            return;
        }

        var subItems = SubItems.ToList();
        if (subItems.Count == 0)
        {
            return;
        }

        var delayByIndexId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ex in profileExs)
        {
            if (ex.IndexId.IsNotEmpty())
            {
                delayByIndexId[ex.IndexId] = ex.Delay;
            }
        }

        _allNodeStates.Clear();
        var delaysBySubId = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var allDelays = new List<int>(allProfiles.Count);
        var activeSubId = string.Empty;
        var hasActiveNode = false;
        var activeIndexId = _config.IndexId ?? string.Empty;

        foreach (var profile in allProfiles)
        {
            if (profile.IndexId.IsNullOrEmpty())
            {
                continue;
            }

            var subId = profile.Subid ?? string.Empty;
            var delay = delayByIndexId.GetValueOrDefault(profile.IndexId, 0);
            _allNodeStates[profile.IndexId] = (subId, delay);
            allDelays.Add(delay);

            if (!delaysBySubId.TryGetValue(subId, out var subDelays))
            {
                subDelays = [];
                delaysBySubId[subId] = subDelays;
            }
            subDelays.Add(delay);

            if (activeIndexId.IsNotEmpty() && string.Equals(profile.IndexId, activeIndexId, StringComparison.Ordinal))
            {
                hasActiveNode = true;
                activeSubId = subId;
            }
        }

        _hasActiveServer = hasActiveNode;
        _activeServerSubId = activeSubId;

        var remoteCount = 0;
        var enabledRemoteCount = 0;
        long totalUp = 0;
        long totalDown = 0;
        foreach (var sub in subItems)
        {
            if (sub.Id.IsNotEmpty() && (sub.Url.IsNotEmpty() || sub.MoreUrl.IsNotEmpty()))
            {
                remoteCount++;
                if (sub.Enabled)
                {
                    enabledRemoteCount++;
                }
                totalUp += Math.Max(0L, sub.TrafficUpload);
                totalDown += Math.Max(0L, sub.TrafficDownload);
            }
        }
        var allSummary = new SubscriptionAllSummary(remoteCount, enabledRemoteCount, totalUp, totalDown);

        DateTimeOffset? latestAnySpeedtest = _subLastSpeedtestAt.TryGetValue(string.Empty, out var rootTs)
            ? rootTs
            : null;
        foreach (var sub in subItems)
        {
            var id = sub.Id ?? string.Empty;
            if (id.IsNullOrEmpty())
            {
                continue;
            }

            if (!_subLastSpeedtestAt.TryGetValue(id, out var subTs))
            {
                var snapshotTs = SubscriptionSnapshotHandler.TryGetSnapshotTimestamp(id);
                if (snapshotTs is { } snap)
                {
                    _subLastSpeedtestAt[id] = snap;
                    subTs = snap;
                }
            }

            if (subTs != default && (latestAnySpeedtest is null || subTs > latestAnySpeedtest.Value))
            {
                latestAnySpeedtest = subTs;
            }
        }

        var nowLocal = DateTimeOffset.Now;
        var existingById = new Dictionary<string, SubscriptionCardItem>(StringComparer.Ordinal);
        foreach (var existing in SubscriptionCards)
        {
            existingById.TryAdd(existing.Id, existing);
        }

        var sameOrder = SubscriptionCards.Count == subItems.Count
            && SubscriptionCards.Select(c => c.Id).SequenceEqual(subItems.Select(s => s.Id ?? string.Empty), StringComparer.Ordinal);

        var updatedCards = sameOrder ? null : new List<SubscriptionCardItem>(subItems.Count);
        for (var i = 0; i < subItems.Count; i++)
        {
            var sub = subItems[i];
            var id = sub.Id ?? string.Empty;
            var card = sameOrder
                ? SubscriptionCards[i]
                : existingById.GetValueOrDefault(id) ?? new SubscriptionCardItem();

            SubscriptionNodeMetrics metrics;
            bool isActiveConfig;
            if (id.IsNullOrEmpty())
            {
                metrics = SubscriptionCardItem.BuildNodeMetrics(allDelays, latestAnySpeedtest);
                isActiveConfig = hasActiveNode;
            }
            else
            {
                var subDelays = delaysBySubId.TryGetValue(id, out var list) ? list : [];
                var lastTs = _subLastSpeedtestAt.TryGetValue(id, out var ts) ? ts : (DateTimeOffset?)null;
                metrics = SubscriptionCardItem.BuildNodeMetrics(subDelays, lastTs);
                isActiveConfig = hasActiveNode && string.Equals(activeSubId, id, StringComparison.Ordinal);
            }

            card.Apply(sub, metrics, isActiveConfig, allSummary, nowLocal);
            updatedCards?.Add(card);
        }

        _syncingSubSelection = true;
        try
        {
            if (updatedCards is not null)
            {
                SubscriptionCards.ReplaceRange(updatedCards);
            }

            var selectedSubId = SelectedSub?.Id ?? _config.SubIndexId ?? string.Empty;
            var targetCard = SubscriptionCards.FirstOrDefault(c => string.Equals(c.Id, selectedSubId, StringComparison.Ordinal))
                ?? SubscriptionCards.FirstOrDefault();
            if (!ReferenceEquals(SelectedSubscriptionCard, targetCard))
            {
                SelectedSubscriptionCard = targetCard;
            }
        }
        finally
        {
            _syncingSubSelection = false;
        }
    }

    private void UpdateCardNodeDelayInPlace(string indexId, string? fallbackSubId, int delay)
    {
        string subId;
        if (_allNodeStates.TryGetValue(indexId, out var existing))
        {
            subId = existing.SubId;
        }
        else
        {
            subId = fallbackSubId ?? string.Empty;
        }

        _allNodeStates[indexId] = (subId, delay);

        var now = DateTimeOffset.Now;
        _subLastSpeedtestAt[string.Empty] = now;
        if (subId.IsNotEmpty())
        {
            _subLastSpeedtestAt[subId] = now;
        }

        var allCard = SubscriptionCards.FirstOrDefault(c => c.IsAllGroup);
        if (allCard is not null)
        {
            var allMetrics = SubscriptionCardItem.BuildNodeMetrics(_allNodeStates.Values.Select(v => v.Delay), now);
            allCard.ApplyNodeMetrics(allMetrics, now);
        }

        if (subId.IsNotEmpty())
        {
            var subCard = SubscriptionCards.FirstOrDefault(c => string.Equals(c.Id, subId, StringComparison.Ordinal));
            if (subCard is not null)
            {
                var subDelays = _allNodeStates.Values
                    .Where(v => string.Equals(v.SubId, subId, StringComparison.Ordinal))
                    .Select(v => v.Delay);
                var subMetrics = SubscriptionCardItem.BuildNodeMetrics(subDelays, now);
                subCard.ApplyNodeMetrics(subMetrics, now);
            }
        }
    }

    private void RefreshActiveConfigBadgesFromCache()
    {
        if (SubscriptionCards.Count == 0)
        {
            return;
        }

        var activeIndexId = _config.IndexId ?? string.Empty;
        if (activeIndexId.IsNotEmpty() && _allNodeStates.TryGetValue(activeIndexId, out var entry))
        {
            _hasActiveServer = true;
            _activeServerSubId = entry.SubId;
        }
        else if (activeIndexId.IsNullOrEmpty())
        {
            _hasActiveServer = false;
            _activeServerSubId = string.Empty;
        }

        foreach (var card in SubscriptionCards)
        {
            card.IsActiveConfig = card.IsAllGroup
                ? _hasActiveServer
                : _hasActiveServer && string.Equals(card.Id, _activeServerSubId, StringComparison.Ordinal);
        }
    }

    private void UpdateSelectedSubscriptionTraffic(SubItem? item)
    {
        if (item is null || item.Id.IsNullOrEmpty() || (item.Url.IsNullOrEmpty() && item.MoreUrl.IsNullOrEmpty()))
        {
            SelectedSubscriptionTraffic = string.Empty;
            HasSelectedSubscriptionTraffic = false;
            return;
        }

        var used = Math.Max(0, item.TrafficUpload) + Math.Max(0, item.TrafficDownload);
        var total = item.TrafficTotal > 0 ? FormatGigabytes(item.TrafficTotal) : "∞";
        var remaining = item.TrafficTotal > 0
            ? FormatGigabytes(Math.Max(0, item.TrafficTotal - used))
            : "∞";

        SelectedSubscriptionTraffic = $"共有流量 {total}　剩余流量 {remaining}　已用流量 {FormatGigabytes(used)}";
        HasSelectedSubscriptionTraffic = true;
    }

    private static string FormatGigabytes(long bytes)
    {
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / bytesPerGigabyte:#,##0.##} GB";
    }

    public async Task AdjustMainLvColWidth()
    {
        await AdjustMainLvColWidthInteraction.HandleSafe(RxVoid.Default);
    }

    private async Task<List<ProfileItemModel>?> GetProfileItemsEx(string subid, string filter)
    {
        var lstModel = await AppManager.Instance.ProfileModels(_config.SubIndexId, filter);

        await ConfigHandler.SetDefaultServer(_config, lstModel);

        var lstServerStat = (_config.GuiItem.EnableStatistics ? StatisticsManager.Instance.ServerStat : null) ?? [];
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        lstModel = (from t in lstModel
                    join t2 in lstServerStat on t.IndexId equals t2.IndexId into t2b
                    from t22 in t2b.DefaultIfEmpty()
                    join t3 in lstProfileExs on t.IndexId equals t3.IndexId into t3b
                    from t33 in t3b.DefaultIfEmpty()
                    select new ProfileItemModel
                    {
                        IndexId = t.IndexId,
                        ConfigType = t.ConfigType,
                        Remarks = t.Remarks,
                        Address = t.Address,
                        Port = t.Port,
                        //Security = t.Security,
                        Network = t.Network,
                        StreamSecurity = t.StreamSecurity,
                        Subid = t.Subid,
                        SubRemarks = t.SubRemarks,
                        IsActive = t.IndexId == _config.IndexId,
                        Sort = t33?.Sort ?? 0,
                        Delay = t33?.Delay ?? 0,
                        Speed = t33?.Speed ?? 0,
                        DelayVal = t33?.Delay != 0 ? $"{t33?.Delay}" : string.Empty,
                        SpeedVal = t33?.Speed > 0 ? $"{t33?.Speed}" : t33?.Message ?? string.Empty,
                        IpInfo = t33?.IpInfo ?? string.Empty,
                        TodayDown = t22 == null ? "" : Utils.HumanFy(t22.TodayDown),
                        TodayUp = t22 == null ? "" : Utils.HumanFy(t22.TodayUp),
                        TotalDown = t22 == null ? "" : Utils.HumanFy(t22.TotalDown),
                        TotalUp = t22 == null ? "" : Utils.HumanFy(t22.TotalUp)
                    }).OrderBy(t => t.Sort).ToList();

        return lstModel;
    }

    #endregion Servers && Groups

    #region Add Servers

    private async Task<List<ProfileItem>?> GetProfileItems(bool latest)
    {
        var lstSelected = new List<ProfileItem>();
        if (SelectedProfiles == null || SelectedProfiles.Count <= 0)
        {
            return null;
        }

        var orderProfiles = SelectedProfiles?.OrderBy(t => t.Sort);
        if (latest)
        {
            lstSelected.AddRange(await AppManager.Instance.GetProfileItemsOrderedByIndexIds(orderProfiles.Select(sp => sp?.IndexId)));
        }
        else
        {
            lstSelected = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(orderProfiles));
        }

        return lstSelected;
    }

    public async Task EditServerAsync()
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return;
        }
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var eConfigType = item.ConfigType;

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
            await RefreshServers();
            if (item.IndexId == _config.IndexId)
            {
                Reload();
            }
        }
    }

    public async Task RemoveServerAsync()
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }
        if (await ShowYesNoInteraction.HandleSafe(ResUI.RemoveServer) == false)
        {
            return;
        }
        var exists = lstSelected.Exists(t => t.IndexId == _config.IndexId);

        await ConfigHandler.RemoveServers(_config, lstSelected);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        if (lstSelected.Count == ProfileItems.Count)
        {
            ProfileItems.Clear();
        }
        await RefreshServers();
        if (exists)
        {
            Reload();
        }
    }

    private async Task RemoveDuplicateServer()
    {
        if (await ShowYesNoInteraction.HandleSafe(ResUI.RemoveServer) == false)
        {
            return;
        }

        var tuple = await ConfigHandler.DedupServerList(_config, _config.SubIndexId);
        if (tuple.Item1 > 0 || tuple.Item2 > 0)
        {
            await RefreshServers();
            Reload();
        }
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2));
    }

    private async Task CopyServer()
    {
        var lstSelected = await GetProfileItems(false);
        if (lstSelected == null)
        {
            return;
        }
        if (await ConfigHandler.CopyServer(_config, lstSelected) == 0)
        {
            await RefreshServers();
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    public async Task SetDefaultServer()
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return;
        }
        await SetDefaultServer(SelectedProfile.IndexId);
    }

    public async Task SetDefaultServer(string? indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return;
        }
        if (indexId == _config.IndexId)
        {
            return;
        }
        var item = await AppManager.Instance.GetProfileItem(indexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        if (await ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
        {
            await RefreshServers();
            Reload();
        }
    }

    public async Task ShareServerAsync()
    {
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var url = FmtHandler.GetShareUri(item);
        if (url.IsNullOrEmpty())
        {
            return;
        }

        await ShareServerInteraction.HandleSafe(url);
    }

    private async Task GenGroupAllServer()
    {
        var ret = await ConfigHandler.AddGroupAllServer(_config, SelectedSub);
        if (ret.Success != true)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }
        _pendingSelectIndexId = ret.Data?.ToString();
        await RefreshServers();
    }

    private async Task GenGroupRegionServer()
    {
        var ret = await ConfigHandler.AddGroupRegionServer(_config, SelectedSub);
        if (ret.Success != true)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }
        var indexIdList = ret.Data as List<string>;
        _pendingSelectIndexId = indexIdList?.FirstOrDefault();
        await RefreshServers();
    }

    private async Task GenerateCloudflareBestNodes()
    {
        if (IsCloudflareOptimizing)
        {
            NoticeManager.Instance.Enqueue("优选中...");
            return;
        }

        var template = await AppManager.Instance.GetProfileItem(SelectedProfile?.IndexId);
        if (template is null)
        {
            NoticeManager.Instance.Enqueue("请选择用于生成最优节点的模板节点。");
            return;
        }

        IsCloudflareOptimizing = true;
        NoticeManager.Instance.SendMessageAndEnqueue("优选中...");

        try
        {
            SendCloudflareBestLog($"开始自动生成最优节点，模板：{template.GetSummary()}，当前分组：{_config.SubIndexId}");
            var cleanup = await CleanupOldCloudflareBestNodes();
            SendCloudflareBestLog($"旧最优节点清理完成：保留 {cleanup.KeptCount} 个，删除 {cleanup.RemovedCount} 个。");
            var addCount = Math.Max(0, CloudflareBestNodeLimit - cleanup.KeptCount);
            var addedIndexIds = new List<string>();
            SendCloudflareBestLog($"最优节点池上限 {CloudflareBestNodeLimit} 个，本次需要补齐 {addCount} 个。");

            if (addCount > 0)
            {
                _cloudflareSpeedTestService ??= new CloudflareSpeedTestService();
                SendCloudflareBestLog("开始启动 CloudflareST。");
                var cfstResult = await _cloudflareSpeedTestService.RunAsync(addCount);
                if (cfstResult.Success)
                {
                    SendCloudflareBestLog($"CloudflareST 返回 {cfstResult.Records.Count} 条候选记录，开始生成节点。");
                    addedIndexIds = await AddCloudflareBestNodes(template, cfstResult.Records, addCount);
                    SendCloudflareBestLog($"最优节点生成完成：新增 {addedIndexIds.Count} 个。");
                }
                else
                {
                    SendCloudflareBestLog($"CloudflareST 执行失败：{cfstResult.Message}");
                    NoticeManager.Instance.Enqueue(cfstResult.Message);
                }
            }
            else
            {
                NoticeManager.Instance.Enqueue($"当前分组已保留 {CloudflareBestNodeLimit} 个可用最优节点。");
            }

            await RefreshServers();

            var profiles = await GetCurrentGroupProfilesOrdered();
            if (profiles.Count > 0)
            {
                SendCloudflareBestLog($"开始对当前分组 {profiles.Count} 个节点执行真延迟测试、排序并设置活动节点。");
                await RunSpeedtestOnce(ESpeedActionType.Realping, profiles, async () => await CleanupCloudflareBestNodesSortAndSetFirstServer(profiles), displayCoreLog: false);
                SendCloudflareBestLog("真延迟测试、排序和活动节点设置流程完成。");
            }

            NoticeManager.Instance.Enqueue($"优选完成：保留 {cleanup.KeptCount} 个，删除 {cleanup.RemovedCount} 个，新增 {addedIndexIds.Count} 个。");
            SendCloudflareBestLog($"优选完成：保留 {cleanup.KeptCount} 个，删除 {cleanup.RemovedCount} 个，新增 {addedIndexIds.Count} 个。");
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Generate Cloudflare best nodes failed", ex);
            NoticeManager.Instance.Enqueue($"优选失败：{ex.Message}");
            SendCloudflareBestLog($"优选失败：{ex.Message}");
        }
        finally
        {
            IsCloudflareOptimizing = false;
            SendCloudflareBestLog("自动生成最优节点流程结束。");
        }
    }

    private async Task<CloudflareBestCleanupResult> CleanupOldCloudflareBestNodes()
    {
        var oldNodes = await GetCurrentGroupCloudflareBestNodes();
        if (oldNodes.Count == 0)
        {
            SendCloudflareBestLog("当前分组没有旧的最优节点，跳过清理。");
            return new(0, 0);
        }

        SendCloudflareBestLog($"发现旧最优节点 {oldNodes.Count} 个，开始执行真延迟测试。");
        await RunSpeedtestOnce(ESpeedActionType.Realping, oldNodes, displayCoreLog: false);

        var profileExs = await ProfileExManager.Instance.GetProfileExs();
        var delayMap = profileExs
            .Where(t => t.IndexId.IsNotEmpty())
            .GroupBy(t => t.IndexId)
            .ToDictionary(t => t.Key, t => t.First().Delay);

        var removeList = oldNodes
            .Where(t =>
            {
                delayMap.TryGetValue(t.IndexId, out var delay);
                return delay == -1 || delay > CloudflareBestNodeMaxDelay;
            })
            .ToList();

        if (removeList.Count > 0)
        {
            SendCloudflareBestLog($"删除不可用旧最优节点 {removeList.Count} 个。");
            await ConfigHandler.RemoveServers(_config, removeList);
        }

        return new(oldNodes.Count - removeList.Count, removeList.Count);
    }

    private async Task<List<ProfileItem>> GetCurrentGroupCloudflareBestNodes()
    {
        var items = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
        return items
            .Where(t => string.Equals(t.Remarks, CloudflareBestNodeRemark, StringComparison.Ordinal))
            .ToList();
    }

    private async Task<List<ProfileItem>> GetCurrentGroupProfilesOrdered()
    {
        var profiles = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
        var profileExs = await ProfileExManager.Instance.GetProfileExs();
        var sortMap = profileExs
            .Where(t => t.IndexId.IsNotEmpty())
            .GroupBy(t => t.IndexId)
            .ToDictionary(t => t.Key, t => t.First().Sort);

        return profiles
            .OrderBy(t => sortMap.GetValueOrDefault(t.IndexId))
            .ToList();
    }

    private async Task<List<string>> AddCloudflareBestNodes(ProfileItem template, IReadOnlyList<CloudflareSpeedTestRecord> records, int addCount)
    {
        var addedIndexIds = new List<string>();
        if (records.Count == 0 || addCount <= 0)
        {
            return addedIndexIds;
        }

        var currentGroupItems = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
        var templateGroupItems = string.Equals(template.Subid, _config.SubIndexId, StringComparison.Ordinal)
            ? currentGroupItems
            : await AppManager.Instance.ProfileItems(template.Subid) ?? [];

        var usedAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in currentGroupItems.Concat(templateGroupItems))
        {
            if (item.Address.IsNotEmpty())
            {
                usedAddresses.Add(item.Address);
            }
        }

        foreach (var record in records)
        {
            if (addedIndexIds.Count >= addCount)
            {
                break;
            }
            if (!usedAddresses.Add(record.IpAddress))
            {
                continue;
            }

            var profileItem = JsonUtils.DeepCopy(template);
            if (profileItem is null)
            {
                continue;
            }

            profileItem.IndexId = string.Empty;
            profileItem.Remarks = CloudflareBestNodeRemark;
            profileItem.Address = record.IpAddress;
            profileItem.Subid = template.Subid;
            ApplyCloudflareTemplateHost(profileItem, template);

            if (await ConfigHandler.AddServerCommon(_config, profileItem, true) == 0
                && profileItem.IndexId.IsNotEmpty())
            {
                addedIndexIds.Add(profileItem.IndexId);
            }
        }

        return addedIndexIds;
    }

    private static void SendCloudflareBestLog(string message)
    {
        Logging.SaveLog(message);
        NoticeManager.Instance.SendMessageEx(message);
    }

    private static void ApplyCloudflareTemplateHost(ProfileItem profileItem, ProfileItem template)
    {
        var templateTransport = template.GetTransportExtra();
#pragma warning disable CS0618
        var templateHost = templateTransport.Host.NullIfEmpty()
                           ?? template.RequestHost.NullIfEmpty()
                           ?? template.Address.NullIfEmpty()
                           ?? string.Empty;

        profileItem.RequestHost = templateHost;
#pragma warning restore CS0618
        profileItem.Sni = template.Sni.NullIfEmpty() ?? templateHost;
        profileItem.SetTransportExtra(profileItem.GetTransportExtra() with { Host = templateHost });
    }

    private sealed record CloudflareBestCleanupResult(int KeptCount, int RemovedCount);

    public async Task SortServer(string colName, bool? ascOverride = null)
    {
        if (colName.IsNullOrEmpty())
        {
            return;
        }

        _dicHeaderSort.TryAdd(colName, true);
        _dicHeaderSort.TryGetValue(colName, out var asc);
        var sortAsc = ascOverride ?? asc;
        if (await ConfigHandler.SortServers(_config, _config.SubIndexId, colName, sortAsc) != 0)
        {
            return;
        }
        await ProfileExManager.Instance.SaveTo();
        _dicHeaderSort[colName] = !sortAsc;
        await RefreshServers();
    }

    public async Task RemoveInvalidServerResult()
    {
        var count = await ConfigHandler.RemoveInvalidServerResult(_config, _config.SubIndexId);
        await RefreshServers();
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveInvalidServerResultTip, count));
    }

    //move server
    private async Task MoveToGroup()
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        await ConfigHandler.MoveToGroup(_config, lstSelected, SelectedMoveToGroup.Id);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);

        await RefreshServers();
        SelectedMoveToGroup = null;
        SelectedMoveToGroup = new();
    }

    public async Task MoveServer(EMove eMove)
    {
        var lstProfile = ProfileItems?.Select(t => t.IndexId).ToList() ?? [];
        var index = lstProfile.IndexOf(SelectedProfile.IndexId);
        if (index < 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        if (await ConfigHandler.MoveServer(_config, lstProfile, index, eMove) == 0)
        {
            await RefreshServers();
        }
    }

    public async Task MoveServerTo(int startIndex, ProfileItemModel targetItem)
    {
        var targetIndex = ProfileItems.IndexOf(targetItem);
        if (startIndex >= 0 && targetIndex >= 0 && startIndex != targetIndex)
        {
            var lstProfile = ProfileItems?.Select(t => t.IndexId).ToList() ?? [];
            if (await ConfigHandler.MoveServer(_config, lstProfile, startIndex, EMove.Position, targetIndex) == 0)
            {
                await RefreshServers();
            }
        }
    }

    private async Task<bool> AutoRealPingAndSort()
    {
        await RefreshServers();
        Logging.SaveLog($"Automatic subscription workflow is starting a real latency test. Subscription={_config.SubIndexId}, Nodes={ProfileItems?.Count ?? 0}.");
        return await ServerSpeedtest(ESpeedActionType.Realping, sortAfterComplete: true, testAll: true);
    }

    public async Task<SubscriptionLatencyTestResult> TestAndSortSubscriptions(IReadOnlyCollection<string> subscriptionIds)
    {
        var ids = subscriptionIds
            .Where(id => id.IsNotEmpty())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var profiles = new List<ProfileItem>();
        foreach (var id in ids)
        {
            var items = await AppManager.Instance.ProfileItems(id) ?? [];
            profiles.AddRange(items.Where(IsSpeedtestCandidate));
        }

        Logging.SaveLog($"Subscription real latency workflow started. Subscriptions={ids.Count}, Nodes={profiles.Count}.");
        var delays = await MeasureRealDelays(profiles, showResults: true);

        foreach (var id in ids)
        {
            await ConfigHandler.SortServers(_config, id, nameof(EServerColName.DelayVal), true);
        }
        await ProfileExManager.Instance.SaveTo();

        var result = await ReadSubscriptionLatencyResult(ids, profiles.Count, delays);
        AppEvents.ProfilesRefreshRequested.Publish();
        Logging.SaveLog($"Subscription real latency workflow completed. Tested={profiles.Count}, Best={result.BestProfile?.IndexId ?? Global.None}, Delay={result.BestProfile?.Delay ?? -1}.");
        return result;
    }

    internal static async Task<SubscriptionLatencyTestResult> ReadSubscriptionLatencyResult(
        IReadOnlyList<string> ids, int tested, IReadOnlyDictionary<string, int> delays)
    {
        var available = new HashSet<string>(StringComparer.Ordinal);
        ProfileItemModel? best = null;
        foreach (var id in ids)
        {
            var models = await AppManager.Instance.ProfileModels(id, "") ?? [];
            foreach (var model in models)
            {
                if (!delays.TryGetValue(model.IndexId, out var delay) || delay <= 0)
                {
                    continue;
                }
                available.Add(id);
                if (best is null || delay < best.Delay)
                {
                    model.Delay = delay;
                    best = model;
                }
            }
        }
        return new SubscriptionLatencyTestResult(ids, tested, best, available);
    }

    internal async Task<List<string>> TestLocalAlternatives(string? activeId)
    {
        var profiles = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
        var candidates = profiles.Where(item => item.IndexId != activeId && IsSpeedtestCandidate(item)).ToList();
        var delays = await MeasureRealDelays(candidates);
        AppEvents.ProfilesRefreshRequested.Publish();
        return LatencySortHelper.ReachableNodeIds(delays);
    }

    private async Task<IReadOnlyDictionary<string, int>> MeasureRealDelays(List<ProfileItem> candidates, bool showResults = false)
    {
        var delays = new ConcurrentDictionary<string, int>();
        if (candidates.Count == 0)
        {
            return delays;
        }

        var speedtest = new SpeedtestService(_config, result =>
        {
            if (result.IndexId is { Length: > 0 } id && int.TryParse(result.Delay, out var delay))
            {
                delays[id] = delay;
            }
            return showResults ? DispatchSpeedTestResult(result) : Task.CompletedTask;
        });
        await speedtest.RunOnceAsync(ESpeedActionType.Realping, candidates, displayCoreLog: false);
        return delays;
    }

    private static bool IsSpeedtestCandidate(ProfileItem item)
    {
        return item.IndexId.IsNotEmpty()
            && item.ConfigType != EConfigType.Custom
            && (item.ConfigType.IsComplexType() || item.Port > 0);
    }

    private async Task<bool> SortDelayAndSetFirstServer()
    {
        var activeIndexId = _config.IndexId;
        var keepCurrentServer = await AppManager.Instance.GetProfileItem(activeIndexId) is not null
            && TunTakeoverPolicy.IsEffectiveTakeoverActive(_config);

        await SortServer(nameof(EServerColName.DelayVal), true);
        Logging.SaveLog($"Automatic subscription latency test completed and nodes were sorted in ascending order. Subscription={_config.SubIndexId}.");

        if (keepCurrentServer
            && _config.IndexId == activeIndexId
            && await AppManager.Instance.GetProfileItem(activeIndexId) is not null
            && TunTakeoverPolicy.IsEffectiveTakeoverActive(_config))
        {
            Logging.SaveLog($"Keeping the current active server because automatic system proxy is already enabled. Server={activeIndexId}.");
            return _activateServer is not null && await _activateServer(activeIndexId);
        }

        var lstModel = await GetProfileItemsEx(_config.SubIndexId, string.Empty);
        var first = lstModel?.FirstOrDefault(t => t.IndexId.IsNotEmpty()
            && t.ConfigType != EConfigType.Custom
            && (t.ConfigType.IsComplexType() || t.Port > 0)
            && t.Delay > 0);

        if (first?.IndexId.IsNotEmpty() == true)
        {
            Logging.SaveLog($"Selecting the lowest-latency available server. Server={first.IndexId}, Delay={first.Delay} ms.");
            return _activateServer is not null && await _activateServer(first.IndexId);
        }

        const string message = "延迟测试没有找到可用节点，未切换活动节点，也未启用自动系统代理。";
        NoticeManager.Instance.Enqueue(message);
        Logging.SaveLog(message);
        return false;
    }

    private async Task CleanupCloudflareBestNodesSortAndSetFirstServer(IReadOnlyList<ProfileItem> testedProfiles)
    {
        var removedCount = await CleanupUnavailableCloudflareBestNodes(testedProfiles);
        if (removedCount > 0)
        {
            NoticeManager.Instance.Enqueue($"最优节点测速已删除不可用节点 {removedCount} 个");
            await RefreshServers();
        }

        await SortDelayAndSetFirstServer();
    }

    private async Task<int> CleanupUnavailableCloudflareBestNodes(IReadOnlyList<ProfileItem> testedProfiles)
    {
        var testedIds = testedProfiles
            .Where(t => t.IndexId.IsNotEmpty()
                && t.Remarks == CloudflareBestNodeRemark
                && t.ConfigType != EConfigType.Custom
                && (t.ConfigType.IsComplexType() || t.Port > 0))
            .Select(t => t.IndexId)
            .Distinct()
            .ToList();

        if (testedIds.Count == 0)
        {
            return 0;
        }

        var profileMap = await AppManager.Instance.GetProfileItemsByIndexIdsAsMap(testedIds);
        var testedIdSet = testedIds.ToHashSet(StringComparer.Ordinal);
        var profileExs = await ProfileExManager.Instance.GetProfileExs();
        var delayMap = profileExs
            .Where(t => t.IndexId.IsNotEmpty() && testedIdSet.Contains(t.IndexId))
            .GroupBy(t => t.IndexId)
            .ToDictionary(t => t.Key, t => t.First().Delay);

        var removeList = new List<ProfileItem>();
        foreach (var id in testedIds)
        {
            if (!profileMap.TryGetValue(id, out var profile))
            {
                continue;
            }

            if (profile.Remarks == CloudflareBestNodeRemark
                && delayMap.TryGetValue(id, out var delay)
                && (delay == -1 || delay > CloudflareBestNodeMaxDelay))
            {
                removeList.Add(profile);
            }
        }

        if (removeList.Count == 0)
        {
            return 0;
        }

        await ConfigHandler.RemoveServers(_config, removeList);
        return removeList.Count;
    }

    private SpeedtestService GetSpeedtestService()
    {
        return _speedtestService ??= new SpeedtestService(_config, DispatchSpeedTestResult);
    }

    private Task DispatchSpeedTestResult(SpeedTestResult result)
    {
        RxSchedulers.MainThreadScheduler.Schedule(() => { _ = SetSpeedTestResult(result); });
        return Task.CompletedTask;
    }

    private async Task RunSpeedtestOnce(ESpeedActionType actionType, List<ProfileItem> selecteds, Func<Task>? completedFunc = null, bool displayCoreLog = true)
    {
        if (selecteds.Count <= 0)
        {
            return;
        }

        await GetSpeedtestService().RunOnceAsync(actionType, selecteds, completedFunc, displayCoreLog);
    }

    public async Task<bool> ServerSpeedtest(ESpeedActionType actionType, bool sortAfterComplete = false, bool testAll = false)
    {
        List<ProfileItem>? lstSelected;
        if (actionType == ESpeedActionType.FastRealping)
        {
            actionType = ESpeedActionType.Realping;
            testAll = true;
        }

        if (actionType == ESpeedActionType.Mixedtest || testAll)
        {
            lstSelected = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(ProfileItems?.OrderBy(t => t.Sort)));
        }
        else
        {
            lstSelected = await GetProfileItems(false);
        }

        if (lstSelected is null || lstSelected.Count <= 0)
        {
            return false;
        }

        await GetSpeedtestService().RunLoop(actionType, lstSelected);
        return !sortAfterComplete || await SortDelayAndSetFirstServer();
    }

    public void ServerSpeedtestStop()
    {
        _speedtestService?.ExitLoop();
    }

    private async Task Export2ClientConfigAsync(bool blClipboard)
    {
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        var (context, validatorResult) = await CoreConfigContextBuilder.Build(_config, item);
        if (NoticeManager.Instance.NotifyValidatorResult(validatorResult) && !validatorResult.Success)
        {
            return;
        }

        if (blClipboard)
        {
            var result = await CoreConfigHandler.GenerateClientConfig(context, null);
            if (result.Success != true)
            {
                NoticeManager.Instance.Enqueue(result.Msg);
            }
            else
            {
                await SetClipboardDataInteraction.HandleSafe((string)result.Data);
                NoticeManager.Instance.SendMessage(ResUI.OperationSuccess);
            }
        }
        else
        {
            await SaveFileDialogInteraction.HandleSafe(item);
        }
    }

    public async Task Export2ClientConfigResult(string fileName, ProfileItem item)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }
        var (context, validatorResult) = await CoreConfigContextBuilder.Build(_config, item);
        if (NoticeManager.Instance.NotifyValidatorResult(validatorResult) && !validatorResult.Success)
        {
            return;
        }
        var result = await CoreConfigHandler.GenerateClientConfig(context, fileName);
        if (result.Success != true)
        {
            NoticeManager.Instance.Enqueue(result.Msg);
        }
        else
        {
            NoticeManager.Instance.SendMessageAndEnqueue(string.Format(ResUI.SaveClientConfigurationIn, fileName));
        }
    }

    public async Task Export2ShareUrlAsync(bool blEncode)
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        StringBuilder sb = new();
        foreach (var it in lstSelected)
        {
            var url = FmtHandler.GetShareUri(it);
            if (url.IsNullOrEmpty())
            {
                continue;
            }
            sb.Append(url);
            sb.AppendLine();
        }
        if (sb.Length > 0)
        {
            if (blEncode)
            {
                await SetClipboardDataInteraction.HandleSafe(Utils.Base64Encode(sb.ToString()));
            }
            else
            {
                await SetClipboardDataInteraction.HandleSafe(sb.ToString());
            }
            NoticeManager.Instance.SendMessage(ResUI.BatchExportURLSuccessfully);
        }
    }

    public async Task Export2InnerUrlAsync()
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        var result = string.Empty;

        await Task.Run(() =>
        {
            result = InnerFmt.ToUri(lstSelected);
        });

        if (!result.IsNullOrEmpty())
        {
            await SetClipboardDataInteraction.HandleSafe(result);
            NoticeManager.Instance.SendMessage(ResUI.BatchExportURLSuccessfully);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }

    #endregion Add Servers

    #region Subscription

    public async Task AddSubscription()
    {
        await EditSubAsync(true);
    }

    public async Task EditCurrentSubscription()
    {
        await EditSubAsync(false);
    }

    private async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(_config.SubIndexId);
            if (item is null)
            {
                return;
            }
        }
        var subEditViewModel = new SubEditViewModel(item);
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(subEditViewModel) == true)
        {
            if (blNew && item.Id.IsNotEmpty())
            {
                _config.SubIndexId = item.Id;
            }
            await RefreshSubscriptions();
            await SubSelectedChangedAsync();
            if (blNew && item.Id.IsNotEmpty() && item.Url.IsNotEmpty())
            {
                AppEvents.CurrentSubscriptionUpdateRequested.Publish(item.Id);
            }
        }
    }

    private async Task DeleteSubAsync()
    {
        var item = await AppManager.Instance.GetSubItem(_config.SubIndexId);
        if (item is null)
        {
            return;
        }

        if (await ShowYesNoInteraction.HandleSafe(ResUI.RemoveServer) == false)
        {
            return;
        }
        await ConfigHandler.DeleteSubItem(_config, item.Id);

        await RefreshSubscriptions();
        await SubSelectedChangedAsync();
    }

    #endregion Subscription
}
