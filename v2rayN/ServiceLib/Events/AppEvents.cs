namespace ServiceLib.Events;

public static class AppEvents
{
    public static readonly EventChannel<RxVoid> AddServerViaClipboardRequested = new();
    public static readonly EventChannel<RxVoid> AddSubscriptionRequested = new();
    public static readonly EventChannel<bool> SubscriptionsUpdateRequested = new();
    public static readonly EventChannel<string> CurrentSubscriptionUpdateRequested = new();
    public static readonly EventChannel<bool> HasUpdateNotified = new();

    public static readonly EventChannel<RxVoid> ProfilesRefreshRequested = new();
    public static readonly EventChannel<RxVoid> SubscriptionsRefreshRequested = new();
    public static readonly EventChannel<string> SubscriptionSelectionChanged = new();
    public static readonly EventChannel<RxVoid> SubscriptionAutoSpeedtestRequested = new();
    public static readonly EventChannel<RxVoid> OneClickNetworkSetupRequested = new();
    public static readonly EventChannel<RxVoid> EditCurrentSubscriptionRequested = new();
    public static readonly EventChannel<NetworkAvailabilityInfo> NetworkAvailabilityChanged = new();
    public static readonly EventChannel<RxVoid> ProxiesReloadRequested = new();
    public static readonly EventChannel<ServerSpeedItem> DispatcherStatisticsRequested = new();

    public static readonly EventChannel<string> SendSnackMsgRequested = new();
    public static readonly EventChannel<string> SendMsgViewRequested = new();

    public static readonly EventChannel<RxVoid> AppExitRequested = new();
    public static readonly EventChannel<bool> ShutdownRequested = new();

    public static readonly EventChannel<ESysProxyType> SysProxyChangeRequested = new();
}
