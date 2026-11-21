using System.Diagnostics;

namespace Game_Daily_Routine_Launcher;

internal sealed class TaskProcessSession(Process? launched, string monitoredProcessName) : ITaskProcessSession
{
    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        launched?.WaitForExitAsync(cancellationToken) ??
        throw new InvalidOperationException("启动方式未返回程序进程，请配置监控进程名以使用日志监听模式。");

    public Task TerminateAsync() => Task.Run(async () =>
    {
        if (launched is not null && !launched.HasExited)
        {
            await KillAndWaitAsync(launched);
            return;
        }

        if (string.IsNullOrEmpty(monitoredProcessName))
            return;

        var processes = Process.GetProcessesByName(monitoredProcessName);
        try { await Task.WhenAll(processes.Select(KillAndWaitAsync)); }
        finally { foreach (var process in processes) process.Dispose(); }
    });

    private static async Task KillAndWaitAsync(Process process)
    {
        if (process.Id == Environment.ProcessId)
            throw new InvalidOperationException("无法将 MAAA 本身作为任务终止目标。");

        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (System.ComponentModel.Win32Exception) when (process.HasExited) { }
        catch (InvalidOperationException) when (process.HasExited) { }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"未能确认进程 {process.Id} 已退出。");
        }
    }

    public void Dispose() => launched?.Dispose();
}
