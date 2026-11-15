using System.Windows;

namespace Game_Daily_Routine_Launcher;

public partial class WebhookRelayHelpWindow : Window
{
    public WebhookRelayHelpWindow()
    {
        InitializeComponent();
        ThemeService.ObserveWindow(this);
        DataContext ??= new WebhookRelayHelpViewModel();
    }

    private async void OnCopyForwardUrlClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WebhookRelayHelpViewModel vm)
            return;

        var copied = await ClipboardService.TrySetTextAsync(vm.ForwardUrlExample, retryCount: 4, delayMs: 25);
        if (!copied)
            MessageBox.Show("复制失败，请稍后重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public sealed class WebhookRelayHelpViewModel
{
    public WebhookRelayHelpViewModel(int port = 5058, string? sourceUrl = null)
    {
        SourceUrlExample = string.IsNullOrWhiteSpace(sourceUrl)
            ? "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=你的key"
            : sourceUrl;
        ForwardUrlExample = BuildForwardUrlExample(port, SourceUrlExample);
    }

    public string SourceUrlExample { get; }

    public string ForwardUrlExample { get; }

    private static string BuildForwardUrlExample(int port, string sourceUrl)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return $"http://127.0.0.1:{port}{uri.PathAndQuery}";

        return $"http://127.0.0.1:{port}/原路径?原参数";
    }
}
