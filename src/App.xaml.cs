using System.Windows;

namespace Game_Daily_Routine_Launcher;

public partial class App : Application
{
    public static bool IsAutoRun { get; private set; }
    public static UdpElevationRequest? UdpResumeRequest { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RuntimeLogService.Initialize();
        RuntimeLogService.WriteMessage($"启动参数：{string.Join(' ', e.Args)}");

        DispatcherUnhandledException += (_, args) =>
        {
            RuntimeLogService.WriteException("发生未处理的异常", args.Exception);
            MessageBox.Show($"发生未处理的异常:\n{args.Exception.Message}",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        IsAutoRun = e.Args.Contains("--autorun", StringComparer.OrdinalIgnoreCase);

        try
        {
            UdpResumeRequest = UdpElevationRequest.Parse(e.Args);
            if (UdpResumeRequest is not null)
            {
                UdpProxyPaths.RequireAdministrator();
                await UdpElevationService.WaitForParentExitAsync(UdpResumeRequest.ParentProcessId);
            }
        }
        catch (Exception ex)
        {
            RuntimeLogService.WriteException("恢复 UDP 提权操作失败", ex);
            MessageBox.Show(ex.Message, "恢复 UDP 操作失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        if (UdpResumeRequest is null && ShouldExitSilentlyForAutoRun())
        {
            RuntimeLogService.WriteMessage("自动运行启动，但当前不在允许的定时启动窗口，程序静默退出。");
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
        if (UdpResumeRequest is not null && MainWindow.DataContext is MainViewModel vm)
            await vm.ResumeUdpOperationAsync(UdpResumeRequest);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RuntimeLogService.WriteMessage($"程序退出，退出码：{e.ApplicationExitCode}。");
        base.OnExit(e);
    }

    private static bool ShouldExitSilentlyForAutoRun()
    {
        if (!IsAutoRun)
            return false;

        var config = new ConfigService().Load();
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return !config.IsInScheduledTimeRange(now);
    }
}
