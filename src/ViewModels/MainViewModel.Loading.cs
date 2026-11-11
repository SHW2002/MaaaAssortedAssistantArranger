namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private void LoadConfig()
    {
        _isLoading = true;

        Tasks.Clear();
        foreach (var taskConfig in _appConfig.Tasks)
        {
            var vm = new GameTaskViewModel(taskConfig) { ConfigChanged = SaveConfig };
            Tasks.Add(vm);
        }

        LoadBasicConfig();
        LoadScreenRecordingConfig();
        LoadAiSummaryConfig();
        LoadWebhookConfig();

        var migration = SystemService.EnsureAutoRunUsesScheduledTask();
        AutoRunOnStart = SystemService.IsAutoRunRegistered();
        IsMuted = _audioService.IsMuted;
        RefreshRecentAiSummaryPromptLogAvailability();
        LoadWebhookPushContentOptions();
        RefreshTwinkleTrayAvailability();

        _isLoading = false;

        if (migration.changed && !string.IsNullOrWhiteSpace(migration.message))
            AddLog(migration.message);

        if (App.IsAutoRun && RandomStartEnabled)
            _randomAutoStartTime = _appConfig.GetRandomScheduledStartTime(DateTime.Now, Random.Shared);

        if (App.IsAutoRun && ShouldStartForAutoRun(DateTime.Now))
            QueueAutoStart();

        if (MuteOnStart && (!MuteOnlyOnAutoRun || App.IsAutoRun))
            StartStartupMuteEnforcement();

        if (TwinkleTrayOnStart && (!TwinkleTrayOnlyOnAutoRun || App.IsAutoRun))
            StartStartupTwinkleTrayEnforcement();

        if (CloseFlClashBeforeRun)
            _startupFlClashTask = CloseStartupFlClashAsync();

        RefreshWebhookRelayState();

        _scheduleTimer.Interval = TimeSpan.FromSeconds(Math.Max(PollIntervalSeconds, 1));
        _scheduleTimer.Start();
        IsSchedulePolling = true;
    }

    private void LoadBasicConfig()
    {
        CloseFlClashBeforeRun = _appConfig.CloseFlClashBeforeRun;
        MuteOnStart = _appConfig.MuteOnStart;
        MuteOnlyOnAutoRun = _appConfig.MuteOnlyOnAutoRun;
        RestoreVolumeOnCompletion = _appConfig.RestoreVolumeOnCompletion;
        RestoreBrightnessOnCompletion = _appConfig.RestoreBrightnessOnCompletion;
        ShutdownOnComplete = _appConfig.ShutdownOnComplete;
        RandomStartEnabled = _appConfig.RandomStartEnabled;
        ScheduledHour = _appConfig.ScheduledHour;
        ScheduledMinute = _appConfig.ScheduledMinute;
        ScheduledEndHour = _appConfig.ScheduledEndHour;
        ScheduledEndMinute = _appConfig.ScheduledEndMinute;
        PollIntervalSeconds = _appConfig.PollIntervalSeconds;
        TwinkleTrayOnStart = _appConfig.TwinkleTrayOnStart;
        TwinkleTrayOnlyOnAutoRun = _appConfig.TwinkleTrayOnlyOnAutoRun;
        ShutdownOnlyOnAutoRun = _appConfig.ShutdownOnlyOnAutoRun;
    }

    private void LoadScreenRecordingConfig()
    {
        var config = _appConfig.ScreenRecording ?? new ScreenRecordingConfig();
        ScreenRecordingEnabled = config.Enabled;
        ScreenRecordingExpanded = config.Expanded;
        ScreenRecordingResolution = Enum.IsDefined(config.Resolution)
            ? config.Resolution
            : ScreenRecordingResolution.Hd720p;
        ScreenRecordingFramerate = ScreenRecordingProfile.NormalizeFramerate(config.Framerate);
        ScreenRecordingRecordSystemAudio = config.RecordSystemAudio;
    }

    private void LoadAiSummaryConfig()
    {
        AiSummaryEnabled = _appConfig.AiSummary.Provider != AiSummaryProviderType.Off;
        AiSummaryOnlyOnAutoRun = _appConfig.AiSummaryOnlyOnAutoRun;
        AiSummaryExpanded = _appConfig.AiSummaryExpanded;
        SelectedAiSummaryProvider = _appConfig.AiSummary.Provider == AiSummaryProviderType.Off
            ? AiSummaryProviderType.ZhipuAi
            : _appConfig.AiSummary.Provider;
        ZhipuApiKey = _appConfig.AiSummary.ZhipuAi.ApiKey;
        ZhipuApiUrl = _appConfig.AiSummary.ZhipuAi.ApiUrl;
        ZhipuModel = _appConfig.AiSummary.ZhipuAi.Model;
        ZhipuThinkingEnabled = _appConfig.AiSummary.ZhipuAi.ThinkingEnabled;
        LoadZhipuProxyConfig(_appConfig.AiSummary.ZhipuAi.Proxy);
        ChatGptApiKey = _appConfig.AiSummary.ChatGpt.ApiKey;
        ChatGptApiUrl = AiSummaryEndpointResolver.Resolve(
            AiSummaryProviderType.ChatGpt,
            _appConfig.AiSummary.ChatGpt.ApiUrl);
        ChatGptModel = _appConfig.AiSummary.ChatGpt.Model;
        LoadChatGptProxyConfig(_appConfig.AiSummary.ChatGpt.Proxy);
        DeepSeekApiKey = _appConfig.AiSummary.DeepSeek.ApiKey;
        DeepSeekApiUrl = AiSummaryEndpointResolver.Resolve(
            AiSummaryProviderType.DeepSeek,
            _appConfig.AiSummary.DeepSeek.ApiUrl);
        DeepSeekModel = _appConfig.AiSummary.DeepSeek.Model;
        LoadDeepSeekProxyConfig(_appConfig.AiSummary.DeepSeek.Proxy);
        AiSystemPrompt = _appConfig.AiSummary.Common.SystemPrompt;
        AiSummaryPrompt = string.IsNullOrWhiteSpace(_appConfig.AiSummary.Common.SummaryPrompt)
            ? AiSummaryCommonConfig.DefaultSummaryPrompt
            : _appConfig.AiSummary.Common.SummaryPrompt;
        AiTemperature = _appConfig.AiSummary.Common.Temperature;
        AiTimeoutSeconds = _appConfig.AiSummary.Common.TimeoutSeconds;
        AiRequestRetryCount = _appConfig.AiSummary.Common.RequestRetryCount;
        AiRequestRetryDelaySeconds = Math.Clamp(_appConfig.AiSummary.Common.RequestRetryDelaySeconds, 0, 86_400);
        AiStream = _appConfig.AiSummary.Common.Stream;
        RefreshSelectedAiProviderProperties();
    }

    private void LoadZhipuProxyConfig(AiSummaryProxyConfig? proxy)
    {
        ZhipuProxyEnabled = proxy?.Enabled == true;
        ZhipuProxyUrl = proxy?.Url ?? string.Empty;
        ZhipuProxyUsername = proxy?.Username ?? string.Empty;
        ZhipuProxyPassword = proxy?.Password ?? string.Empty;
    }

    private void LoadChatGptProxyConfig(AiSummaryProxyConfig? proxy)
    {
        ChatGptProxyEnabled = proxy?.Enabled == true;
        ChatGptProxyUrl = proxy?.Url ?? string.Empty;
        ChatGptProxyUsername = proxy?.Username ?? string.Empty;
        ChatGptProxyPassword = proxy?.Password ?? string.Empty;
    }

    private void LoadDeepSeekProxyConfig(AiSummaryProxyConfig? proxy)
    {
        DeepSeekProxyEnabled = proxy?.Enabled == true;
        DeepSeekProxyUrl = proxy?.Url ?? string.Empty;
        DeepSeekProxyUsername = proxy?.Username ?? string.Empty;
        DeepSeekProxyPassword = proxy?.Password ?? string.Empty;
    }

    private void LoadWebhookConfig()
    {
        WebhookEnabled = _appConfig.WebhookEnabled;
        WebhookUrl = _appConfig.WebhookUrl;
        WebhookBody = _appConfig.WebhookBody;
        WebhookRelayEnabled = _appConfig.WebhookRelayEnabled;
        WebhookRelayPort = _appConfig.WebhookRelayPort;
        WebhookRelaySourceUrl = _appConfig.WebhookRelaySourceUrl;
        WebhookOnlyPushAiSummary = _appConfig.WebhookOnlyPushAiSummary;
        WebhookCustomPushContentEnabled = _appConfig.WebhookCustomPushContentEnabled;
        WebhookEnabledExpanded = _appConfig.WebhookEnabledExpanded;
        WebhookRelayExpanded = _appConfig.WebhookRelayExpanded;
        WebhookCustomPushContentExpanded = _appConfig.WebhookCustomPushContentExpanded;
    }
}
