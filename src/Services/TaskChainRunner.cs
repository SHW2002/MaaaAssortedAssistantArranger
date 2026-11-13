namespace Game_Daily_Routine_Launcher;

public partial class TaskChainRunner : IDisposable
{
    private readonly AudioService _audio;
    private readonly ITaskProcessService _processes;
    private readonly IUdpProxyService _udpProxy;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _completion;
    private bool _disposed;

    public Action<IReadOnlyList<GameTaskConfig>>? ValidateTasks { get; init; }
    public Func<CancellationToken, Task>? BeforeTaskAsync { get; init; }
    public Func<Task>? AfterChainAsync { get; init; }
    public event Action<string, string>? TaskStateChanged;
    public event Action<string>? LogMessage;
    public bool IsRunning { get; private set; }

    public TaskChainRunner(AudioService audio, int pollIntervalMs = 60000,
        IUdpProxyService? udpProxy = null, ITaskProcessService? processes = null)
    {
        _audio = audio;
        _processes = processes ?? new TaskProcessService(pollIntervalMs);
        _udpProxy = udpProxy ?? new BuiltinUdpProxyService(Log);
    }

    public async Task<bool> RunChainAsync(IReadOnlyList<GameTaskConfig> tasks, bool muteOnStart)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) throw new InvalidOperationException("任务链已在运行。");
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IsRunning = true;
        var failed = false;
        try
        {
            foreach (var task in tasks.Where(task => task.Enabled)) UdpProxyRules.Validate(task);
            ValidateTasks?.Invoke(tasks);
            if (BeforeTaskAsync is not null) await BeforeTaskAsync(_cts.Token);
            if (muteOnStart)
            {
                _audio.SetMute(true);
                Log("系统音量已静音。");
            }
            foreach (var task in tasks.Where(task => task.Enabled))
            {
                _cts.Token.ThrowIfCancellationRequested();
                if (!await RunSingleTaskAsync(task, _cts.Token)) failed = true;
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
                if (AfterChainAsync is not null) await AfterChainAsync();
            }
            catch (Exception ex)
            {
                failed = true;
                Log($"任务链收尾失败：{ex.Message}");
            }
            finally
            {
                IsRunning = false;
                _completion.TrySetResult();
                if (_disposed) _cts.Dispose();
            }
        }
        return !failed && !_cts.IsCancellationRequested;
    }

    public void Stop()
    {
        if (!_disposed) _cts?.Cancel();
    }

    public async Task StopAsync()
    {
        Stop();
        if (_completion is not null) await _completion.Task;
    }

    private void Log(string message) => LogMessage?.Invoke(message);

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        if (!IsRunning) _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
