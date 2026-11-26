using System.Diagnostics;

namespace Game_Daily_Routine_Launcher;

internal sealed class TaskProcessSession(Process? launched, string monitoredProcessName, string[] relatedProcessNames)
    : ITaskProcessSession
{
    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        launched?.WaitForExitAsync(cancellationToken) ??
        throw new InvalidOperationException("启动方式未返回程序进程，请填写监控进程以判断任务何时结束。");

    public Task TerminateAsync() => Task.Run(async () =>
    {
        var failures = new List<Exception>();
        try
        {
            if (launched is not null && !launched.HasExited)
                await KillAndWaitAsync(launched, entireProcessTree: true);
            else if (monitoredProcessName.Length > 0)
                await TerminateNamedAsync(monitoredProcessName, entireProcessTree: true);
        }
        catch (Exception ex) { failures.Add(ex); }

        var related = Task.WhenAll(relatedProcessNames.Select(name => TerminateNamedAsync(name, entireProcessTree: false)));
        try { await related; }
        catch (Exception ex) { failures.Add(related.Exception ?? ex); }

        if (failures.Count > 0)
            throw new AggregateException("有进程未能关闭。", failures).Flatten();
    });

    private static async Task TerminateNamedAsync(string name, bool entireProcessTree)
    {
        try
        {
            var processes = Process.GetProcessesByName(name);
            try { await Task.WhenAll(processes.Select(process => KillAndWaitAsync(process, entireProcessTree))); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"关闭进程 {name} 失败：{ex.Message}", ex);
        }
    }

    private static async Task KillAndWaitAsync(Process process, bool entireProcessTree)
    {
        if (process.Id == Environment.ProcessId)
            throw new InvalidOperationException("无法将 MAAA 本身作为任务终止目标。");

        try { if (!process.HasExited) process.Kill(entireProcessTree); }
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
