using System.Dynamic;

namespace Game_Daily_Routine_Launcher.Tests;

internal sealed class AppearancePreviewData : DynamicObject
{
    private readonly Dictionary<string, object?> _values = new()
    {
        ["CanEditTasks"] = true, ["TwinkleTrayIsAvailable"] = true,
        ["TestAutoMuteButtonText"] = "测试", ["TestTwinkleTrayButtonText"] = "测试",
        ["ScheduledHour"] = 4, ["ScheduledMinute"] = 0, ["ScheduledEndHour"] = 6,
        ["ScheduledEndMinute"] = 0, ["PollIntervalSeconds"] = 60, ["Theme"] = ThemeMode.Dark,
        ["IsSchedulePolling"] = true, ["AutoScrollLogs"] = true,
        ["MuteOnStart"] = true, ["RestoreVolumeOnCompletion"] = true, ["RestoreBrightnessOnCompletion"] = true,
        ["WebhookEnabledExpanded"] = true, ["AiSummaryExpanded"] = false,
        ["WebhookRelayStatusMessage"] = "中转服务未启动", ["TwinkleTrayAvailabilityMessage"] = "",
        ["Tasks"] = new[]
        {
            new GameTaskViewModel(new GameTaskConfig
            {
                Id = "march7th", Name = "星穹铁道 (March7th)", ToolPath = "preview://starrail",
                LaunchMode = LaunchMode.Uri, GameProcessName = "StarRail", TimeoutMinutes = 60,
                UdpProxy = new() { Enabled = true }
            }) { IsConfigExpanded = true },
            new GameTaskViewModel(new GameTaskConfig { Id = "maa", Name = "明日方舟 (MAA)" })
        },
        ["LogEntries"] = new[]
        {
            new LogEntryRecord(DateTime.Today.AddHours(4), "配置已加载。"),
            new LogEntryRecord(DateTime.Today.AddHours(4).AddMinutes(1), "等待任务启动...")
        }
    };

    public AppearancePreviewData()
    {
        foreach (var name in new[] { "MuteOnlyOnAutoRun", "TwinkleTrayOnStart", "TwinkleTrayOnlyOnAutoRun",
                     "ShutdownOnComplete", "ShutdownOnlyOnAutoRun", "CloseFlClashBeforeRun", "ScreenRecordingEnabled",
                     "WebhookEnabled", "WebhookRelayEnabled", "AiSummaryEnabled", "RandomStartEnabled", "AutoRunOnStart" })
            _values.TryAdd(name, false);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = _values.GetValueOrDefault(binder.Name);
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _values[binder.Name] = value;
        return true;
    }
}
