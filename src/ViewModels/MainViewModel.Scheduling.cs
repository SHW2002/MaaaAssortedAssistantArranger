namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private void QueueAutoStart()
    {
        _hasRunInCurrentWindow = true;
        _autoSummaryRequested = true;
        StartAllCommand.Execute(null);
    }

    private void OnScheduleTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;

        if (!IsInScheduledTimeRange(now))
        {
            _hasRunInCurrentWindow = false;
            RefreshRandomAutoStartTimeIfNeeded(now);
            return;
        }

        if (!IsRunning && !_hasRunInCurrentWindow && ShouldStartForAutoRun(now))
        {
            _hasRunInCurrentWindow = true;
            QueueAutoStart();
        }
    }

    private bool ShouldStartForAutoRun(DateTime now)
    {
        if (!IsInScheduledTimeRange(now))
            return false;

        return !RandomStartEnabled || _randomAutoStartTime is null || now >= _randomAutoStartTime.Value;
    }

    private void RefreshRandomAutoStartTimeIfNeeded(DateTime now)
    {
        if (!App.IsAutoRun || !RandomStartEnabled)
            return;

        if (_randomAutoStartTime is null || now >= _randomAutoStartTime.Value)
            _randomAutoStartTime = _appConfig.GetRandomScheduledStartTime(now, Random.Shared);
    }

    private bool IsInScheduledTimeRange(DateTime now) =>
        _appConfig.IsInScheduledTimeRange(TimeOnly.FromDateTime(now));

    partial void OnPollIntervalSecondsChanged(int value)
    {
        _scheduleTimer.Interval = TimeSpan.FromSeconds(Math.Max(value, 1));
    }
}
