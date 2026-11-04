using System.Windows.Media;

namespace v2rayN.Views;

public partial class SubscriptionInfoView
{
    public SubscriptionInfoView()
    {
        InitializeComponent();
        ViewModel = new SubscriptionInfoViewModel();

        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.SubscriptionName, v => v.txtSubscriptionName.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Announcement, v => v.txtAnnouncement.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.TotalTraffic, v => v.txtTotalTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RemainingTraffic, v => v.txtRemainingTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.UsedTraffic, v => v.txtUsedTraffic.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ExpireDate, v => v.txtExpireDate.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.LastUpdated, v => v.txtLastUpdated.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.HasSupportUrl, v => v.btnSupport.Visibility).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.NetworkStatusText, v => v.txtNetworkStatus.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.NetworkStatusDetail, v => v.txtNetworkStatusDetail.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.NetworkStatusColor, v => v.networkStatusAccent.Background,
                color => (Brush)new BrushConverter().ConvertFromString(color)!).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.NetworkStatusColor, v => v.networkStatusDot.Fill,
                color => (Brush)new BrushConverter().ConvertFromString(color)!).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.NetworkStatusColor, v => v.networkStatusIcon.Foreground,
                color => (Brush)new BrushConverter().ConvertFromString(color)!).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.ActiveNodeName, v => v.txtActiveNodeName.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.CurrentDelay, v => v.txtCurrentDelay.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.SystemProxyStatus, v => v.txtSystemProxyStatus.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.CanContactSupport, v => v.btnContactSupport.Visibility).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenSupportCmd, v => v.btnSupport).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ContactSupportCmd, v => v.btnContactSupport).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OneClickNetworkSetupCmd, v => v.btnOneClickNetworkSetup).DisposeWith(disposables);
        });
    }
}
