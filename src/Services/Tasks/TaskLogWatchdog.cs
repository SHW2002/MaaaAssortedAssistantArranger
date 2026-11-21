namespace Game_Daily_Routine_Launcher;

internal sealed class TaskLogWatchdog : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _clock;
    private readonly ITimer _timer;
    private readonly TaskCompletionSource _expired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _lastActivity;
    private bool _disposed;

    public TaskLogWatchdog(TimeSpan timeout, TimeProvider clock)
    {
        _timeout = timeout;
        _clock = clock;
        _lastActivity = clock.GetTimestamp();
        _timer = clock.CreateTimer(_ => CheckDeadline(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(timeout, Timeout.InfiniteTimeSpan);
    }

    public Task Expired => _expired.Task;

    public void ReportActivity()
    {
        lock (_gate)
        {
            var now = _clock.GetTimestamp();
            if (IsExpired(now))
                return;

            _lastActivity = now;
            _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
        }
    }

    private void CheckDeadline()
    {
        lock (_gate)
        {
            var now = _clock.GetTimestamp();
            if (!IsExpired(now))
                _timer.Change(_timeout - _clock.GetElapsedTime(_lastActivity, now), Timeout.InfiniteTimeSpan);
        }
    }

    private bool IsExpired(long now)
    {
        if (_disposed || _expired.Task.IsCompleted)
            return true;

        if (_clock.GetElapsedTime(_lastActivity, now) < _timeout)
            return false;

        _expired.TrySetResult();
        return true;
    }

    public void Dispose()
    {
        lock (_gate)
            _disposed = true;

        _timer.Dispose();
    }
}
