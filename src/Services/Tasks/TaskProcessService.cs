using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher;

public sealed class TaskProcessService(int pollIntervalMs) : ITaskProcessService
{
    public ITaskProcessSession Launch(GameTaskConfig task)
    {
        var psi = new ProcessStartInfo { UseShellExecute = true };
        if (task.LaunchMode == LaunchMode.Uri)
            psi.FileName = task.ToolPath;
        else
        {
            var fullPath = Path.GetFullPath(task.ToolPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"工具路径不存在：{fullPath}", fullPath);
            psi.FileName = fullPath;
            psi.WorkingDirectory = Path.GetDirectoryName(fullPath);
            psi.Arguments = task.ToolArgs;
        }
        return new TaskProcessSession(Process.Start(psi), GetProcessName(task));
    }

    public async Task WaitForExitAsync(GameTaskConfig task, Action onStarted, CancellationToken cancellationToken)
    {
        using var monitor = new ProcessMonitor(GetProcessName(task), task.UdpProxy.Enabled ? 500 : pollIntervalMs);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.ProcessStarted += onStarted;
        monitor.ProcessExited += () => completion.TrySetResult();
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        monitor.Start();
        try { await completion.Task; }
        finally { await monitor.StopAsync(); }
    }

    private static string GetProcessName(GameTaskConfig task)
    {
        var name = task.GameProcessName?.Trim() ?? string.Empty;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
