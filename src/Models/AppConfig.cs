namespace Game_Daily_Routine_Launcher;

public class AppConfig
{
    public List<GameTaskConfig> Tasks { get; set; } = [];
    public AiSummaryConfig AiSummary { get; set; } = new();
    public ScreenRecordingConfig ScreenRecording { get; set; } = new();
    public bool CloseFlClashBeforeRun { get; set; }
    public bool MuteOnStart { get; set; } = true;
    public bool MuteOnlyOnAutoRun { get; set; }
    public bool RestoreVolumeOnCompletion { get; set; } = true;
    public bool RestoreBrightnessOnCompletion { get; set; } = true;
    public bool ShutdownOnComplete { get; set; }
    public bool RandomStartEnabled { get; set; }
    public int ScheduledHour { get; set; } = 4;
    public int ScheduledMinute { get; set; }
    public int ScheduledEndHour { get; set; } = 6;
    public int ScheduledEndMinute { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public bool TwinkleTrayOnStart { get; set; }
    public bool TwinkleTrayOnlyOnAutoRun { get; set; }
    public bool ShutdownOnlyOnAutoRun { get; set; }
    public bool WebhookEnabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookBody { get; set; } = "{\"time\":\"__TIME__\",\"content\":\"__CONTENT__\"}";
    public bool WebhookRelayEnabled { get; set; }
    public int WebhookRelayPort { get; set; } = 5058;
    public string WebhookRelaySourceUrl { get; set; } = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=你的key";
    public bool WebhookOnlyPushAiSummary { get; set; }
    public bool WebhookCustomPushContentEnabled { get; set; }
    public List<WebhookPushContentCategory> WebhookPushCategories { get; set; } = [];
    public bool AiSummaryOnlyOnAutoRun { get; set; }
    public bool WebhookEnabledExpanded { get; set; } = true;
    public bool WebhookRelayExpanded { get; set; } = true;
    public bool WebhookCustomPushContentExpanded { get; set; } = true;
    public bool AiSummaryExpanded { get; set; } = true;

    public bool IsInScheduledTimeRange(TimeOnly now)
    {
        var start = new TimeOnly(ScheduledHour, ScheduledMinute);
        var end = new TimeOnly(ScheduledEndHour, ScheduledEndMinute);

        return start <= end
            ? now >= start && now < end
            : now >= start || now < end;
    }

    public DateTime GetRandomScheduledStartTime(DateTime now, Random random)
    {
        var range = GetCurrentOrNextScheduledRange(now);
        var earliest = now > range.Start ? now : range.Start;
        var availableSeconds = Math.Max(0, (int)(range.End - earliest).TotalSeconds);

        if (availableSeconds == 0)
            return earliest;

        return earliest.AddSeconds(random.Next(availableSeconds + 1));
    }

    private (DateTime Start, DateTime End) GetCurrentOrNextScheduledRange(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var startTime = new TimeOnly(ScheduledHour, ScheduledMinute);
        var endTime = new TimeOnly(ScheduledEndHour, ScheduledEndMinute);

        if (startTime <= endTime)
        {
            var start = today.ToDateTime(startTime);
            var end = today.ToDateTime(endTime);

            if (now < end)
                return (start, end);

            return (start.AddDays(1), end.AddDays(1));
        }

        var todayStart = today.ToDateTime(startTime);
        var todayEnd = today.ToDateTime(endTime);

        if (now < todayEnd)
            return (todayStart.AddDays(-1), todayEnd);

        var endForTodayStart = todayEnd.AddDays(1);
        return (todayStart, endForTodayStart);
    }
}
