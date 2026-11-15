using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;

namespace Game_Daily_Routine_Launcher;

public partial class MainWindow : Window
{
    private bool _cleanupCompleted;
    private bool _cleanupInProgress;

    public MainWindow()
    {
        InitializeComponent();
        ThemeService.ObserveWindow(this);

        ((INotifyCollectionChanged)LogListBox.Items).CollectionChanged += (_, _) =>
        {
            if (LogListBox.Items.Count > 0 && (DataContext as MainViewModel)?.AutoScrollLogs == true)
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        };

        Closing += OnClosing;
        Loaded += OnLoaded;
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_cleanupCompleted)
            return;

        e.Cancel = true;
        if (_cleanupInProgress)
            return;

        _cleanupInProgress = true;
        if (DataContext is MainViewModel vm)
            await vm.CleanupAsync();

        _cleanupCompleted = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(hwnd, 0x00000002);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        var monitorWidth = (info.rcWork.Right - info.rcWork.Left) * scaleX;
        var monitorHeight = (info.rcWork.Bottom - info.rcWork.Top) * scaleY;
        var monitorLeft = info.rcWork.Left * scaleX;
        var monitorTop = info.rcWork.Top * scaleY;

        var maxWidth = monitorWidth * 0.85;
        var maxHeight = monitorHeight * 0.95;

        if (Width > maxWidth) Width = maxWidth;
        if (Height > maxHeight) Height = maxHeight;

        Left = monitorLeft + (monitorWidth - Width) / 2;
        Top = monitorTop + (monitorHeight - Height) / 2;

        SizeToContent = SizeToContent.Manual;
    }

    private void OnDeleteTaskClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GameTaskViewModel taskVm } ||
            DataContext is not MainViewModel vm)
            return;

        var result = MessageBox.Show(
            this,
            $"确定要删除任务“{taskVm.Name}”吗？",
            "删除任务",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            vm.RemoveTask(taskVm);
    }

    private async void OnLogListBoxMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LogListBox.SelectedItem is not LogEntryRecord entry)
            return;

        await ClipboardService.TrySetTextAsync(entry.DisplayText);
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}

public class MuteTextConverter : IValueConverter
{
    public static readonly MuteTextConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "取消静音" : "静音";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
