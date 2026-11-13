using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel : ObservableObject
{
    private static readonly HashSet<string> NonConfigProperties =
    [
        nameof(IsRunning),
        nameof(IsPreparingUdpProxy),
        nameof(IsMuted),
        nameof(TestAutoMuteButtonText),
        nameof(TestTwinkleTrayButtonText),
        nameof(IsTwinkleTrayTestBusy),
        nameof(TwinkleTrayIsAvailable),
        nameof(TwinkleTrayAvailabilityMessage),
        nameof(AutoRunOnStart),
        nameof(IsSchedulePolling),
        nameof(WebhookRelayIsRunning),
        nameof(WebhookRelayStatusMessage),
        nameof(AutoScrollLogs),
        nameof(SelectedAiApiKey),
        nameof(SelectedAiApiUrl),
        nameof(SelectedAiModel),
        nameof(SelectedAiProxyEnabled),
        nameof(SelectedAiProxyUrl),
        nameof(SelectedAiProxyUsername),
        nameof(SelectedAiProxyPassword),
        nameof(ScreenRecordingBitrateText)
    ];

    private readonly ConfigService _configService;
    private readonly AudioService _audioService;
    private readonly AiSummaryService _aiSummaryService;
    private readonly AiPromptLogService _aiPromptLogService;
    private readonly TwinkleTrayService _twinkleTrayService;
    private readonly WebhookRelayService _webhookRelayService;
    private readonly ScreenRecordingService _screenRecordingService;
    private readonly DispatcherTimer _scheduleTimer;
    private readonly DispatcherTimer _startupMuteTimer;
    private readonly DispatcherTimer _startupTwinkleTrayTimer;
    private AppConfig _appConfig;
    private TaskChainRunner? _chainRunner;
    private bool _isLoading;
    private bool _hasMuteSnapshot;
    private bool _didAutoMute;
    private bool _originalMuteState;
    private float _originalVolume;
    private bool _isTestAutoMuting;
    private bool _isTwinkleTrayTestDimmed;
    private bool _didDimTwinkleTray;
    private IReadOnlyList<TwinkleTrayMonitorState> _originalTwinkleTrayStates = Array.Empty<TwinkleTrayMonitorState>();
    private bool _hasRunInCurrentWindow;
    private bool _autoSummaryRequested;
    private DateTime? _randomAutoStartTime;
    private int _startupMuteSuccessStreak;
    private int _startupTwinkleTraySuccessStreak;
    private bool _twinkleTrayStartupInProgress;

    public ObservableCollection<GameTaskViewModel> Tasks { get; } = [];
    public ObservableCollection<LogEntryRecord> LogEntries { get; } = [];
    public ObservableCollection<WebhookPushContentCategoryOptionViewModel> WebhookPushContentOptions { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopAllCommand))]
    private bool _isRunning;

    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private string _testAutoMuteButtonText = "测试";
    [ObservableProperty] private string _testTwinkleTrayButtonText = "测试";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestTwinkleTrayCommand))]
    private bool _isTwinkleTrayTestBusy;
    [ObservableProperty] private bool _closeFlClashBeforeRun;
    [ObservableProperty] private bool _muteOnStart;
    [ObservableProperty] private bool _muteOnlyOnAutoRun;
    [ObservableProperty] private bool _restoreVolumeOnCompletion = true;
    [ObservableProperty] private bool _restoreBrightnessOnCompletion = true;
    [ObservableProperty] private bool _shutdownOnComplete;
    [ObservableProperty] private bool _randomStartEnabled;
    [ObservableProperty] private bool _autoRunOnStart;
    [ObservableProperty] private int _scheduledHour;
    [ObservableProperty] private int _scheduledMinute;
    [ObservableProperty] private int _scheduledEndHour;
    [ObservableProperty] private int _scheduledEndMinute;
    [ObservableProperty] private int _pollIntervalSeconds;
    [ObservableProperty] private bool _isSchedulePolling;
    [ObservableProperty] private bool _webhookEnabled;
    [ObservableProperty] private string _webhookUrl = string.Empty;
    [ObservableProperty] private string _webhookBody = string.Empty;
    [ObservableProperty] private bool _webhookRelayEnabled;
    [ObservableProperty] private int _webhookRelayPort = 5058;
    [ObservableProperty] private string _webhookRelaySourceUrl = string.Empty;
    [ObservableProperty] private bool _webhookOnlyPushAiSummary;
    [ObservableProperty] private bool _webhookCustomPushContentEnabled;
    [ObservableProperty] private bool _webhookCustomPushContentExpanded = true;
    [ObservableProperty] private bool _webhookRelayIsRunning;
    [ObservableProperty] private string _webhookRelayStatusMessage = string.Empty;
    [ObservableProperty] private bool _webhookEnabledExpanded = true;
    [ObservableProperty] private bool _webhookRelayExpanded = true;
    [ObservableProperty] private bool _twinkleTrayOnStart;
    [ObservableProperty] private bool _twinkleTrayOnlyOnAutoRun;
    [ObservableProperty] private bool _twinkleTrayIsAvailable;
    [ObservableProperty] private string _twinkleTrayAvailabilityMessage = string.Empty;
    [ObservableProperty] private bool _shutdownOnlyOnAutoRun;
    [ObservableProperty] private bool _autoScrollLogs = true;

    public MainViewModel()
    {
        _flClashService = new FlClashService(message => AddLog(message));
        _configService = new ConfigService();
        _audioService = new AudioService();
        _aiSummaryService = new AiSummaryService();
        _aiPromptLogService = new AiPromptLogService();
        _twinkleTrayService = new TwinkleTrayService();
        _webhookRelayService = new WebhookRelayService();
        _screenRecordingService = new ScreenRecordingService();
        _screenRecordingService.RecordingFailed += message =>
            AddLog($"录屏失败：{message}");
        _appConfig = _configService.Load();

        _scheduleTimer = new DispatcherTimer();
        _scheduleTimer.Tick += OnScheduleTimerTick;

        _startupMuteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _startupMuteTimer.Tick += OnStartupMuteTimerTick;

        _startupTwinkleTrayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _startupTwinkleTrayTimer.Tick += OnStartupTwinkleTrayTimerTick;

        LoadConfig();
    }

    public bool CanRunAiSummaryTest => !IsTestingAiSummary;

    public bool CanRunRecentAiSummaryTest => !IsTestingAiSummary && HasRecentAiSummaryPromptLog;

    public bool CanEditTasks => !IsRunning && !IsPreparingUdpProxy;

    private bool CanStartAll() => !IsRunning && !IsPreparingUdpProxy && !_isCleaningUp;

    private bool CanStopAll() => IsRunning;

    partial void OnIsRunningChanged(bool value)
    {
        PrepareUdpProxyCommand.NotifyCanExecuteChanged();
        StartSingleCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditTasks));
    }
}
