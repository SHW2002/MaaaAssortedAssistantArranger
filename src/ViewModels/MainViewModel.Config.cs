namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private void SaveConfig()
    {
        _appConfig.Tasks = Tasks.Select(t => t.ToConfig()).ToList();
        _appConfig.CloseFlClashBeforeRun = CloseFlClashBeforeRun;
        _appConfig.MuteOnStart = MuteOnStart;
        _appConfig.MuteOnlyOnAutoRun = MuteOnlyOnAutoRun;
        _appConfig.RestoreVolumeOnCompletion = RestoreVolumeOnCompletion;
        _appConfig.RestoreBrightnessOnCompletion = RestoreBrightnessOnCompletion;
        _appConfig.ShutdownOnComplete = ShutdownOnComplete;
        _appConfig.RandomStartEnabled = RandomStartEnabled;
        _appConfig.ScheduledHour = ScheduledHour;
        _appConfig.ScheduledMinute = ScheduledMinute;
        _appConfig.ScheduledEndHour = ScheduledEndHour;
        _appConfig.ScheduledEndMinute = ScheduledEndMinute;
        _appConfig.PollIntervalSeconds = PollIntervalSeconds;
        _appConfig.TwinkleTrayOnStart = TwinkleTrayOnStart;
        _appConfig.TwinkleTrayOnlyOnAutoRun = TwinkleTrayOnlyOnAutoRun;
        _appConfig.ShutdownOnlyOnAutoRun = ShutdownOnlyOnAutoRun;
        _appConfig.WebhookEnabled = WebhookEnabled;
        _appConfig.WebhookUrl = WebhookUrl;
        _appConfig.WebhookBody = WebhookBody;
        _appConfig.WebhookRelayEnabled = WebhookRelayEnabled;
        _appConfig.WebhookRelayPort = WebhookRelayPort;
        _appConfig.WebhookRelaySourceUrl = WebhookRelaySourceUrl;
        _appConfig.WebhookOnlyPushAiSummary = WebhookOnlyPushAiSummary;
        _appConfig.WebhookCustomPushContentEnabled = WebhookCustomPushContentEnabled;
        _appConfig.WebhookEnabledExpanded = WebhookEnabledExpanded;
        _appConfig.WebhookRelayExpanded = WebhookRelayExpanded;
        _appConfig.WebhookCustomPushContentExpanded = WebhookCustomPushContentExpanded;
        _appConfig.AiSummaryExpanded = AiSummaryExpanded;
        _appConfig.ScreenRecording = BuildScreenRecordingConfig();
        _appConfig.WebhookPushCategories = WebhookPushContentOptions
            .Where(x => x.IsEnabled)
            .Select(x => x.Category)
            .ToList();
        _appConfig.AiSummaryOnlyOnAutoRun = AiSummaryOnlyOnAutoRun;
        _appConfig.AiSummary = BuildAiSummaryConfig();
        _configService.Save(_appConfig);
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_isLoading)
            return;

        var propertyName = e.PropertyName ?? string.Empty;
        if (!NonConfigProperties.Contains(propertyName))
            SaveConfig();
    }

    partial void OnAutoRunOnStartChanged(bool value)
    {
        if (_isLoading)
            return;

        var (success, message) = value
            ? SystemService.RegisterAutoRun()
            : SystemService.UnregisterAutoRun();

        AddLog(success
            ? $"开机自启{(value ? "启用" : "取消")}成功：{message}"
            : $"开机自启{(value ? "启用" : "取消")}失败：{message}");
    }

    partial void OnTwinkleTrayOnStartChanged(bool value)
    {
        if (_isLoading)
            return;
    }

    private void RefreshTwinkleTrayAvailability()
    {
        var availability = _twinkleTrayService.DetectAvailability();
        TwinkleTrayIsAvailable = availability.IsAvailable;
        TwinkleTrayAvailabilityMessage = availability.Message;

        if (!availability.IsAvailable)
        {
            if (!_isLoading)
                AddLog($"Twinkle Tray 不可用：{availability.Message}");
        }
    }

    partial void OnWebhookOnlyPushAiSummaryChanged(bool value)
    {
        if (_isLoading)
            return;

        if (value && !AiSummaryEnabled)
            AddLog("未开启 AI 总结服务。");
    }

    partial void OnAiSummaryEnabledChanged(bool value)
    {
        if (_isLoading)
            return;

        if (value && SelectedAiSummaryProvider == AiSummaryProviderType.Off)
            SelectedAiSummaryProvider = AiSummaryProviderType.ZhipuAi;
        else if (!value)
            SelectedAiSummaryProvider = AiSummaryProviderType.ZhipuAi;
    }

    partial void OnAiSummaryOnlyOnAutoRunChanged(bool value)
    {
        if (_isLoading)
            return;

        if (!value && App.IsAutoRun)
            AddLog("AI 智能总结已设置为始终生效。");
    }

    partial void OnAiSummaryPromptChanged(string value)
    {
        if (_isLoading)
            return;

        if (string.IsNullOrWhiteSpace(value))
            AiSummaryPrompt = AiSummaryCommonConfig.DefaultSummaryPrompt;
    }

    partial void OnIsTestingAiSummaryChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunAiSummaryTest));
        OnPropertyChanged(nameof(CanRunRecentAiSummaryTest));
    }

    partial void OnHasRecentAiSummaryPromptLogChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunRecentAiSummaryTest));
    }

    private ScreenRecordingConfig BuildScreenRecordingConfig() => new()
    {
        Enabled = ScreenRecordingEnabled,
        Expanded = ScreenRecordingExpanded,
        Resolution = Enum.IsDefined(ScreenRecordingResolution)
            ? ScreenRecordingResolution
            : ScreenRecordingResolution.Hd720p,
        Framerate = ScreenRecordingProfile.NormalizeFramerate(ScreenRecordingFramerate),
        RecordSystemAudio = ScreenRecordingRecordSystemAudio
    };
}
