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
        ITaskProcessSession? launched = null;
        Task? monitor = null;
        TaskLogWatchdog? watchdog = null;
        var state = TaskState.Completed;
        var hasMonitoredProcess = !string.IsNullOrWhiteSpace(task.GameProcessName);
        var hasRelatedProcesses = TaskProcessNames.ForCleanup(task.RelatedProcessNames).Length > 0;
        var tracksCompletion = hasMonitoredProcess || hasRelatedProcesses || task.LogMonitoringEnabled;
        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            if (task.LogMonitoringEnabled && !GameTaskConfig.IsValidLogInactivityTimeout(task.LogInactivityTimeoutMinutes))
                throw new InvalidOperationException(GameTaskConfig.LogInactivityTimeoutError);

            if (task.UdpProxy.Enabled)
            {
                Log($"正在为 {task.Name} 启动 UDP 代理...");
                proxy = await _udpProxy.StartAsync(task, ct);
                if (proxy.Exited.IsCompleted) throw new InvalidOperationException("UDP 代理已意外退出。");
                Log($"UDP 代理已就绪，仅匹配：{string.Join(", ", UdpProxyRules.Validate(task))}。");
            }
            ct.ThrowIfCancellationRequested();
            if (hasMonitoredProcess || (hasRelatedProcesses && !task.LogMonitoringEnabled))
                monitorCts.CancelAfter(TimeSpan.FromMinutes(task.TimeoutMinutes));

            if (hasMonitoredProcess)
            {
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
            if (task.LogMonitoringEnabled)
            {
                watchdog = new TaskLogWatchdog(TimeSpan.FromMinutes(task.LogInactivityTimeoutMinutes), Clock);
                Volatile.Write(ref _logWatchdog, watchdog);
            }

            launched = _processes.Launch(task);
            if (monitor is null) TaskStateChanged?.Invoke(task.Id, "Running");
            if (tracksCompletion)
            {
                monitor ??= launched.WaitForExitAsync(monitorCts.Token);
                var signals = new List<Task> { monitor };
                if (proxy is not null) signals.Add(proxy.Exited);
                if (watchdog is not null) signals.Add(watchdog.Expired);
                var finished = await Task.WhenAny(signals);
                ct.ThrowIfCancellationRequested();

                if (finished == watchdog?.Expired)
                {
                    Log($"任务 {task.Name} 连续 {task.LogInactivityTimeoutMinutes} 分钟未收到日志，正在强制终止。");
                    state = TaskState.Terminated;
                }
                else
                {
                    if (finished != monitor)
                        throw new InvalidOperationException("UDP 代理运行中意外退出，任务已中止。");
                    await monitor;
                    if (hasMonitoredProcess) Log($"{task.GameProcessName} 进程已退出。");
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Log($"{task.Name} 已超时，超过 {task.TimeoutMinutes} 分钟。");
            state = TaskState.TimedOut;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log($"启动或执行 {task.Name} 失败：{ex.Message}");
            state = TaskState.Error;
        }
        finally
        {
            Volatile.Write(ref _logWatchdog, null);
            watchdog?.Dispose();
            monitorCts.Cancel();
            if (monitor is not null)
            {
                try { await monitor; }
                catch (Exception) { /* The task result was already reported above. */ }
            }
            using (launched)
            {
                if (launched is not null && tracksCompletion)
                {
                    try
                    {
                        await launched.TerminateAsync();
                        if (state == TaskState.Terminated) Log($"任务 {task.Name} 已终止。");
                    }
                    catch (Exception ex)
                    {
                        Log($"清理 {task.Name} 的进程失败：{ex.Message}");
                        if (state is TaskState.Completed or TaskState.Terminated) state = TaskState.Error;
                    }
                }
            }
            if (proxy is not null)
            {
                await proxy.DisposeAsync();
                Log($"{task.Name} 的 UDP 代理已关闭。");
            }
        }
        TaskStateChanged?.Invoke(task.Id, state.ToString());
        if (state == TaskState.Completed) Log($"{task.Name} 已完成。");
        return state == TaskState.Completed;
    }
}
