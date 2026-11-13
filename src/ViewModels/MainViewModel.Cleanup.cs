namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    public Task CleanupAsync() => _cleanupTask ??= CleanupCoreAsync();

    private async Task CleanupCoreAsync()
    {
        _isCleaningUp = true;
        _scheduleTimer.Stop();
        _startupMuteTimer.Stop();
        _startupTwinkleTrayTimer.Stop();
        IsSchedulePolling = false;

        if (_udpPreparationTask is not null)
        {
            try { await _udpPreparationTask; }
            catch (Exception ex) { AddLog($"UDP 组件准备已结束：{ex.Message}"); }
        }
        if (_chainRunner is not null)
            await _chainRunner.StopAsync();
        if (_startupFlClashTask is not null)
            await _startupFlClashTask;
        await _flClashService.RestoreAsync(cleaningUp: true);
        await StopScreenRecordingAsync();
        _screenRecordingService.Dispose();

        if (_startupTwinkleTrayTask is not null)
            await _startupTwinkleTrayTask;
        await _webhookRelayService.StopAsync();
        await RestoreConfiguredStateAsync();
    }
}
