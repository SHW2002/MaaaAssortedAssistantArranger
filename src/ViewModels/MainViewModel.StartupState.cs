namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private Task? _startupTwinkleTrayTask;
    private Task? _cleanupTask;
    private bool _isCleaningUp;

    private void StartStartupMuteEnforcement()
    {
        CaptureMuteSnapshot();
        _didAutoMute = true;
        _startupMuteSuccessStreak = 0;

        _startupMuteTimer.Start();
        EnforceStartupMuteOnce();
    }

    private void StartStartupTwinkleTrayEnforcement()
    {
        _didDimTwinkleTray = true;
        _startupTwinkleTraySuccessStreak = 0;

        _startupTwinkleTrayTimer.Start();
        _startupTwinkleTrayTask = EnforceStartupTwinkleTrayAsync();
    }

    private void OnStartupMuteTimerTick(object? sender, EventArgs e) => EnforceStartupMuteOnce();

    private void OnStartupTwinkleTrayTimerTick(object? sender, EventArgs e)
    {
        _startupTwinkleTrayTask = EnforceStartupTwinkleTrayAsync();
    }

    private void EnforceStartupMuteOnce()
    {
        if (ApplyMute())
        {
            if (++_startupMuteSuccessStreak >= 3)
                _startupMuteTimer.Stop();
        }
        else
        {
            _startupMuteSuccessStreak = 0;
        }
    }

    private async Task EnforceStartupTwinkleTrayAsync()
    {
        if (_isCleaningUp || _twinkleTrayStartupInProgress)
            return;

        _twinkleTrayStartupInProgress = true;
        try
        {
            var result = await Task.Run(() => _twinkleTrayService.DetectAvailability());
            if (_isCleaningUp)
                return;

            if (!result.IsAvailable)
            {
                TwinkleTrayAvailabilityMessage = result.Message;
                return;
            }

            TwinkleTrayIsAvailable = true;
            TwinkleTrayAvailabilityMessage = result.Message;

            if (await ApplyTwinkleTrayDimAsync(result))
            {
                if (++_startupTwinkleTraySuccessStreak >= 3)
                    _startupTwinkleTrayTimer.Stop();
            }
            else
            {
                _startupTwinkleTraySuccessStreak = 0;
            }
        }
        finally
        {
            _twinkleTrayStartupInProgress = false;
        }
    }

    public Task CleanupAsync() => _cleanupTask ??= CleanupCoreAsync();

    private async Task RestoreConfiguredStateAsync()
    {
        if (RestoreVolumeOnCompletion)
            RestoreOriginalMuteState();

        if (RestoreBrightnessOnCompletion)
            await RestoreOriginalTwinkleTrayStateAsync();
    }

    private async Task CleanupCoreAsync()
    {
        _isCleaningUp = true;
        _scheduleTimer.Stop();
        _startupMuteTimer.Stop();
        _startupTwinkleTrayTimer.Stop();
        IsSchedulePolling = false;

        _chainRunner?.Stop();
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

    private void RestoreOriginalMuteState()
    {
        if (!_didAutoMute)
            return;

        if (RestoreMute())
        {
            AddLog("已恢复原始静音状态。");
            _didAutoMute = false;
            _hasMuteSnapshot = false;
        }
    }

    private async Task RestoreOriginalTwinkleTrayStateAsync()
    {
        if (!_didDimTwinkleTray || !TwinkleTrayIsAvailable || _originalTwinkleTrayStates.Count == 0)
            return;

        var result = await Task.Run(() => _twinkleTrayService.DetectAvailability());
        if (!result.IsAvailable)
        {
            AddLog($"恢复亮度失败：{result.Message}");
            return;
        }

        if (await RestoreTwinkleTrayAsync(result))
            AddLog("已恢复原始亮度。");
        else
            AddLog("恢复亮度失败。");
    }

    private bool ApplyMute()
    {
        _didAutoMute = true;
        if (!_hasMuteSnapshot)
            CaptureMuteSnapshot();

        _audioService.SetMute(true);
        _audioService.SetVolume(0f);
        IsMuted = _audioService.IsMuted;
        return IsMuted;
    }

    private bool RestoreMute()
    {
        if (!_hasMuteSnapshot)
            return false;

        _audioService.SetMute(_originalMuteState);
        _audioService.SetVolume(_originalVolume);
        IsMuted = _audioService.IsMuted;
        return IsMuted == _originalMuteState;
    }

    private async Task<bool> ApplyTwinkleTrayDimAsync(TwinkleTrayAvailability availability)
    {
        _didDimTwinkleTray = true;

        if (_originalTwinkleTrayStates.Count == 0)
            _originalTwinkleTrayStates = await Task.Run(() => _twinkleTrayService.CaptureCurrentStates());

        if (_originalTwinkleTrayStates.Count == 0)
            return false;

        var dimResult = await _twinkleTrayService.SetAllLowestAsync(availability);
        if (!dimResult.Success)
        {
            TwinkleTrayAvailabilityMessage = dimResult.Message;
            return false;
        }

        return true;
    }

    private async Task<bool> RestoreTwinkleTrayAsync(TwinkleTrayAvailability availability)
    {
        if (_originalTwinkleTrayStates.Count == 0)
            return false;

        var restoreResult = await _twinkleTrayService.RestoreAsync(availability, _originalTwinkleTrayStates);
        if (!restoreResult.Success)
        {
            TwinkleTrayAvailabilityMessage = restoreResult.Message;
            return false;
        }

        _didDimTwinkleTray = false;
        _originalTwinkleTrayStates = Array.Empty<TwinkleTrayMonitorState>();
        return true;
    }

    private void CaptureMuteSnapshot()
    {
        _originalMuteState = _audioService.IsMuted;
        _originalVolume = _audioService.Volume;
        _hasMuteSnapshot = true;
    }
}
