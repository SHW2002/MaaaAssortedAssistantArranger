using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher;

public static class UdpElevationService
{
    public static bool NeedsElevation(IEnumerable<GameTaskConfig> tasks, bool isAdministrator) =>
        !isAdministrator && tasks.Any(task => task.Enabled && task.UdpProxy.Enabled);

    public static ProcessStartInfo CreateStartInfo(
        string processPath, string assemblyPath, UdpElevationRequest request, bool isAutoRun)
    {
        var info = new ProcessStartInfo(processPath)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(assemblyPath);
        if (isAutoRun) info.ArgumentList.Add("--autorun");
        foreach (var argument in request.ToArguments()) info.ArgumentList.Add(argument);
        return info;
    }

    public static (bool Success, string Message) TryStart(UdpElevationRequest request, bool isAutoRun)
    {
        try
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前程序路径。");
            using var process = Process.Start(CreateStartInfo(path, typeof(App).Assembly.Location, request, isAutoRun));
            return process is null
                ? (false, "无法以管理员权限重新启动程序，原窗口已保留。")
                : (true, "管理员权限已获准，正在清理当前实例并继续原操作...");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "已取消管理员权限请求，本次操作未启动；原窗口已保留，可重试。");
        }
        catch (Exception ex)
        {
            return (false, $"申请管理员权限失败：{ex.Message}；原窗口已保留。");
        }
    }

    public static async Task WaitForParentExitAsync(int processId)
    {
        Process parent;
        try { parent = Process.GetProcessById(processId); }
        catch (ArgumentException) { return; }
        using (parent)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            try { await parent.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("原程序未能在 60 秒内退出，已取消恢复操作，避免重复运行任务。");
            }
        }
    }
}
