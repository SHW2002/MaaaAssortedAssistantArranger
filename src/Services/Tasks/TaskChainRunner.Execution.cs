namespace Game_Daily_Routine_Launcher;

public partial class TaskChainRunner
{
    private async Task<bool> RunSingleTaskAsync(GameTaskConfig task, CancellationToken ct)
    {
        if (task.DelayBeforeStartMs > 0)
        {
            Log($"等待 {task.DelayBeforeStartMs / 1000} 秒后启动 {task.Name}...");
            TaskStateChanged?.Invoke(task.Id, "Launching");
            await Task.Delay(task.DelayBeforeStartMs, ct);
        }
        if (BeforeTaskAsync is not null) await BeforeTaskAsync(ct);
        ct.ThrowIfCancellationRequested();
        TaskStateChanged?.Invoke(task.Id, "Launching");
        IUdpProxySession? proxy = null;
        Task? monitor = null;
        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            if (task.UdpProxy.Enabled)
            {
                Log($"正在为 {task.Name} 启动 UDP 代理...");
                proxy = await _udpProxy.StartAsync(task, ct);
                if (proxy.Exited.IsCompleted) throw new InvalidOperationException("UDP 代理已意外退出。");
                Log($"UDP 代理已就绪，仅匹配：{string.Join(", ", UdpProxyRules.Validate(task))}。");
            }
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(task.GameProcessName))
            {
                monitorCts.CancelAfter(TimeSpan.FromMinutes(task.TimeoutMinutes));
                TaskStateChanged?.Invoke(task.Id, "MonitoringWaitStart");
                monitor = _processes.WaitForExitAsync(task, () =>
                {
                    Log($"{task.GameProcessName} 进程已启动，等待关闭...");
                    TaskStateChanged?.Invoke(task.Id, "MonitoringWaitStop");
                }, monitorCts.Token);
            }
            ct.ThrowIfCancellationRequested();
            if (monitor is { IsFaulted: true }) await monitor;
            Log($"正在启动 {task.Name}...");
            _processes.Launch(task);
            if (monitor is null) TaskStateChanged?.Invoke(task.Id, "Running");
            else
            {
                if (proxy is not null && await Task.WhenAny(monitor, proxy.Exited) == proxy.Exited)
                    throw new InvalidOperationException("UDP 代理运行中意外退出，任务已中止。");
                await monitor;
                Log($"{task.GameProcessName} 进程已退出。");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Log($"{task.Name} 已超时，超过 {task.TimeoutMinutes} 分钟。");
            TaskStateChanged?.Invoke(task.Id, "TimedOut");
            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log($"启动或执行 {task.Name} 失败：{ex.Message}");
            TaskStateChanged?.Invoke(task.Id, "Error");
            return false;
        }
        finally
        {
            monitorCts.Cancel();
            if (monitor is not null)
            {
                try { await monitor; }
                catch (Exception) { /* The task result was already reported above. */ }
            }
            if (proxy is not null)
            {
                await proxy.DisposeAsync();
                Log($"{task.Name} 的 UDP 代理已关闭。");
            }
        }
        TaskStateChanged?.Invoke(task.Id, "Completed");
        Log($"{task.Name} 已完成。");
        return true;
    }
}
