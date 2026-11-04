using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class WorkspaceBackgroundSettingWindow : Window
{
    private readonly Config _config;

    public WorkspaceBackgroundSettingWindow()
    {
        InitializeComponent();

        _config = AppManager.Instance.Config;
        txtBackgroundPath.Text = WorkspaceBackgroundHandler.ResolvePath(_config.UiItem.WorkspaceBackgroundImage);
        sldOpacity.Value = Math.Round(Math.Clamp(_config.UiItem.WorkspaceBackgroundOpacity, 0, 1) * 100);
        UpdateOpacityText();

        btnBrowse.Click += BtnBrowse_Click;
        btnClear.Click += (_, _) => txtBackgroundPath.Text = string.Empty;
        btnSave.Click += BtnSave_Click;
        btnCancel.Click += (_, _) => Close(false);
        sldOpacity.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty)
            {
                UpdateOpacityText();
            }
        };
    }

    private async void BtnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var fileName = await UI.OpenFileDialog(null);
        if (fileName.IsNotEmpty())
        {
            txtBackgroundPath.Text = fileName;
        }
    }

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        string image;
        try
        {
            image = WorkspaceBackgroundHandler.PrepareImage(txtBackgroundPath.Text);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Save workspace background failed", ex);
            NoticeManager.Instance.Enqueue($"背景图片保存失败：{ex.Message}");
            return;
        }

        var oldImage = _config.UiItem.WorkspaceBackgroundImage;
        var oldOpacity = _config.UiItem.WorkspaceBackgroundOpacity;
        _config.UiItem.WorkspaceBackgroundImage = image;
        _config.UiItem.WorkspaceBackgroundOpacity = Math.Clamp(sldOpacity.Value, 0, 100) / 100d;

        if (await ConfigHandler.SaveConfig(_config) != 0)
        {
            _config.UiItem.WorkspaceBackgroundImage = oldImage;
            _config.UiItem.WorkspaceBackgroundOpacity = oldOpacity;
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }

        WorkspaceBackgroundHandler.CleanupUserImages(image);
        Close(true);
    }

    private void UpdateOpacityText()
    {
        txtOpacity.Text = $"{sldOpacity.Value:0}%";
    }
}
