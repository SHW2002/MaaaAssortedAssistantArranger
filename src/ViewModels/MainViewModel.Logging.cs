using System.Diagnostics;
using System.Windows;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private void ExecuteOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private void AddLog(string message, string? rawBody = null, bool pushWebhook = true)
    {
        var entry = CreateLogEntry(message, rawBody);
        ExecuteOnUiThread(() => _ = AppendLog(entry, pushWebhook));
    }

    private static LogEntryRecord CreateLogEntry(string message, string? rawBody = null) =>
        new(DateTime.Now, message)
        {
            RawBody = rawBody,
            Category = InferWebhookPushCategory(message, rawBody)
        };

    private static WebhookPushContentCategory InferWebhookPushCategory(string message, string? rawBody)
    {
        if (ContainsAny(message, "AI总结：", "AI 总结", "AI 智能总结"))
            return WebhookPushContentCategory.AiSummary;

        if (ContainsAny(message, "Webhook", "中转"))
            return WebhookPushContentCategory.Webhook;

        if (ContainsAny(message, "静音", "音量"))
            return WebhookPushContentCategory.Audio;

        if (ContainsAny(message, "亮度", "Twinkle Tray"))
            return WebhookPushContentCategory.Brightness;

        if (ContainsAny(
            message,
            "程序启动",
            "程序退出",
            "自动运行启动",
            "静默退出",
            "启动参数",
            "关机"))
            return WebhookPushContentCategory.PowerIndicator;

        if (ContainsAny(
            message,
            "开机自启",
            "计划任务",
            "管理员权限",
            "系统设置",
            "设置"))
            return WebhookPushContentCategory.System;

        if (ContainsAny(message, "任务链", "任务", "启动", "完成", "停止"))
            return WebhookPushContentCategory.TaskExecution;

        if (ContainsAny(message, "监控", "等待", "超时", "进程"))
            return WebhookPushContentCategory.TaskMonitoring;

        return WebhookPushContentCategory.Other;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));

    private void LoadWebhookPushContentOptions()
    {
        var enabled = new HashSet<WebhookPushContentCategory>(_appConfig.WebhookPushCategories);
        WebhookPushContentOptions.Clear();

        foreach (var option in CreateDefaultWebhookPushContentOptions())
        {
            var isEnabled = enabled.Count == 0 || enabled.Contains(option.Category);
            WebhookPushContentOptions.Add(new WebhookPushContentCategoryOptionViewModel(
                option.Category,
                option.DisplayName,
                isEnabled,
                SaveConfig));
        }
    }

    private static IReadOnlyList<(WebhookPushContentCategory Category, string DisplayName)> CreateDefaultWebhookPushContentOptions() =>
    [
        (WebhookPushContentCategory.TaskExecution, "任务启动/完成"),
        (WebhookPushContentCategory.TaskMonitoring, "进程监控"),
        (WebhookPushContentCategory.Audio, "静音/音量"),
        (WebhookPushContentCategory.Brightness, "亮度调节"),
        (WebhookPushContentCategory.Webhook, "Webhook 推送"),
        (WebhookPushContentCategory.AiSummary, "AI 总结"),
        (WebhookPushContentCategory.PowerIndicator, "开关指示"),
        (WebhookPushContentCategory.System, "系统设置"),
        (WebhookPushContentCategory.Other, "其他日志")
    ];

    private Task AppendLog(LogEntryRecord entry, bool pushWebhook = true)
    {
        LogEntries.Add(entry);
        RuntimeLogService.WriteEntry(entry);
        Debug.WriteLine(entry.DisplayText);
        Console.WriteLine(entry.DisplayText);
        if (!string.IsNullOrWhiteSpace(entry.RawBody))
            Console.WriteLine(entry.RawBody);

        return pushWebhook && ShouldPushLogEntry(entry)
            ? PushWebhookAsync(entry.Message, entry.RawBody, entry.Timestamp.ToString("HH:mm:ss"))
            : Task.CompletedTask;
    }

    private bool ShouldPushLogEntry(LogEntryRecord entry)
    {
        if (!WebhookEnabled || string.IsNullOrWhiteSpace(WebhookUrl))
            return false;

        if (WebhookOnlyPushAiSummary)
            return entry.Message.StartsWith("AI总结：", StringComparison.Ordinal);

        if (WebhookCustomPushContentEnabled)
            return WebhookPushContentOptions.Any(x => x.IsEnabled && x.Category == entry.Category);

        return true;
    }

    private async Task PushWebhookAsync(string message, string? rawBody, string time)
    {
        var content = string.IsNullOrWhiteSpace(rawBody)
            ? message
            : $"{message}\n原始Body：\n{rawBody}";
        var result = await WebhookService.SendAsync(WebhookUrl, WebhookBody, time, content);
        if (result.Success)
        {
            RuntimeLogService.WriteMessage($"Webhook sent: {result.RequestBody}");
            Debug.WriteLine($"Webhook sent: {result.RequestBody}");
        }
    }
}
