using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher;

public class TaskChainRunner : IDisposable
{
    private readonly AudioService _audio;
    public Func<CancellationToken, Task>? BeforeTaskAsync { get; init; }
    public Func<Task>? AfterChainAsync { get; init; }
    private readonly int _pollIntervalMs;
    private CancellationTokenSource? _cts;
    private ProcessMonitor? _currentMonitor;

    public event Action<string, string>? TaskStateChanged;
    public event Action<string>? LogMessage;

    public bool IsRunning { get; private set; }

    public TaskChainRunner(AudioService audio, int pollIntervalMs = 60000)
    {
        _audio = audio;
        _pollIntervalMs = pollIntervalMs;
    }

    public async Task<bool> RunChainAsync(IReadOnlyList<GameTaskConfig> tasks, bool muteOnStart)
    {
        _cts = new CancellationTokenSource();
        IsRunning = true;
        var failed = false;

        try
        {
            if (BeforeTaskAsync is not null)
                await BeforeTaskAsync(_cts.Token);

            if (muteOnStart)
            {
                _audio.SetMute(true);
                Log("系统音量已静音。");
            }

            foreach (var task in tasks)
            {
                if (_cts.Token.IsCancellationRequested)
                    break;

                if (!task.Enabled)
                    continue;

                await RunSingleTaskAsync(task, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            failed = true;
            Log("任务链已被用户停止。");
        }
        catch (Exception ex)
        {
            failed = true;
            Log($"任务链执行失败：{ex.Message}");
        }
        finally
        {
            try
            {
                if (AfterChainAsync is not null)
                    await AfterChainAsync();
            }
            finally { IsRunning = false; }
        }

        return !failed && _cts is { IsCancellationRequested: false };
    }

    private async Task RunSingleTaskAsync(GameTaskConfig task, CancellationToken ct)
    {
        if (task.DelayBeforeStartMs > 0)
        {
            Log($"等待 {task.DelayBeforeStartMs / 1000} 秒后启动 {task.Name}...");
            TaskStateChanged?.Invoke(task.Id, "Launching");
            await Task.Delay(task.DelayBeforeStartMs, ct);
        }

        if (BeforeTaskAsync is not null)
            await BeforeTaskAsync(ct);
        ct.ThrowIfCancellationRequested();

        Log($"正在启动 {task.Name}...");
        TaskStateChanged?.Invoke(task.Id, "Running");

        try
        {
            LaunchTool(task);
        }
        catch (Exception ex)
        {
            Log($"启动 {task.Name} 失败：{ex.Message}");
            TaskStateChanged?.Invoke(task.Id, "Error");
            return;
        }

        if (!string.IsNullOrEmpty(task.GameProcessName))
        {
            Log($"正在监控 {task.GameProcessName} 进程，等待关闭，超时 {task.TimeoutMinutes} 分钟...");
            TaskStateChanged?.Invoke(task.Id, "MonitoringWaitStart");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(task.TimeoutMinutes));

            try
            {
                await WaitForProcessExitAsync(task.Id, task.GameProcessName, timeoutCts.Token);
                Log($"{task.GameProcessName} 进程已退出。");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log($"{task.Name} 已超时，超过 {task.TimeoutMinutes} 分钟。");
                TaskStateChanged?.Invoke(task.Id, "TimedOut");
                return;
            }
        }

        TaskStateChanged?.Invoke(task.Id, "Completed");
        Log($"{task.Name} 已完成。");
    }

    private static void LaunchTool(GameTaskConfig task)
    {
        var psi = new ProcessStartInfo();

        if (task.LaunchMode == LaunchMode.Uri)
        {
            psi.FileName = task.ToolPath;
            psi.UseShellExecute = true;
        }
        else
        {
            var fullPath = Path.GetFullPath(task.ToolPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"工具路径不存在：{fullPath}", fullPath);

            psi.FileName = fullPath;
            psi.WorkingDirectory = Path.GetDirectoryName(fullPath);
            psi.Arguments = task.ToolArgs;
            psi.UseShellExecute = true;
        }

        Process.Start(psi);
    }

    private async Task WaitForProcessExitAsync(string taskId, string processName, CancellationToken ct)
    {
        using var monitor = new ProcessMonitor(processName, _pollIntervalMs);
        _currentMonitor = monitor;
        var tcs = new TaskCompletionSource();

        monitor.ProcessStarted += () =>
        {
            Log($"{processName} 进程已启动，等待关闭...");
            TaskStateChanged?.Invoke(taskId, "MonitoringWaitStop");
        };
        monitor.ProcessExited += () => tcs.TrySetResult();

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        monitor.Start();
        try
        {
            await tcs.Task;
        }
        finally
        {
            if (ReferenceEquals(_currentMonitor, monitor))
                _currentMonitor = null;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _currentMonitor?.Stop();
    }

    private void Log(string message) => LogMessage?.Invoke(message);

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _currentMonitor?.Dispose();
        GC.SuppressFinalize(this);
    }
}
