using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _audioService.SetMute(IsMuted);
        _didAutoMute = false;
        AddLog(IsMuted ? "系统音量已静音。" : "系统音量已恢复。");
    }

    [RelayCommand]
    private void TestAutoMute()
    {
        if (_isTestAutoMuting)
        {
            RestoreMute();
            _isTestAutoMuting = false;
            TestAutoMuteButtonText = "测试";
            AddLog("测试自动静音已结束，音量已恢复。");
        }
        else
        {
            _didAutoMute = true;
            ApplyMute();
            _isTestAutoMuting = true;
            TestAutoMuteButtonText = "还原";
            AddLog("测试自动静音已生效。");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunTwinkleTrayTest))]
    private async Task TestTwinkleTray()
    {
        IsTwinkleTrayTestBusy = true;
        TestTwinkleTrayButtonText = _isTwinkleTrayTestDimmed ? "还原" : "等待";
        AddLog("正在检测 Twinkle Tray...");

        try
        {
            await RunTwinkleTrayTestFlowAsync();
        }
        finally
        {
            IsTwinkleTrayTestBusy = false;
            TestTwinkleTrayButtonText = _isTwinkleTrayTestDimmed ? "还原" : "测试";
        }
    }

    private bool CanRunTwinkleTrayTest() => !IsTwinkleTrayTestBusy;

    private async Task RunTwinkleTrayTestFlowAsync()
    {
        var availability = await DetectTwinkleTrayAvailabilityWithRetryAsync();
        if (!availability.IsAvailable)
        {
            AddLog($"Twinkle Tray 测试失败：{availability.Message}");
            return;
        }

        if (_isTwinkleTrayTestDimmed)
        {
            if (await RestoreTwinkleTrayAsync(availability))
            {
                _isTwinkleTrayTestDimmed = false;
                AddLog("Twinkle Tray 测试已恢复原始亮度。");
            }
            else
            {
                AddLog($"Twinkle Tray 测试恢复失败：{TwinkleTrayAvailabilityMessage}");
            }

            return;
        }

        if (await ApplyTwinkleTrayDimAsync(availability))
        {
            _isTwinkleTrayTestDimmed = true;
            AddLog("Twinkle Tray 测试已将亮度调至最低。");
        }
        else
        {
            AddLog($"Twinkle Tray 测试失败：{TwinkleTrayAvailabilityMessage}");
        }
    }

    private async Task<TwinkleTrayAvailability> DetectTwinkleTrayAvailabilityWithRetryAsync()
    {
        var delaySeconds = 1;
        while (true)
        {
            var availability = await Task.Run(() => _twinkleTrayService.DetectAvailability());
            if (availability.IsAvailable || _isCleaningUp)
                return availability;

            TwinkleTrayAvailabilityMessage = availability.Message;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            delaySeconds = Math.Min(delaySeconds + 1, 5);
        }
    }
    [RelayCommand]
    private void ClearLogs()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            var folder = Directory.CreateDirectory(RuntimeLogService.LogDirectory);
            using var process = Process.Start(new ProcessStartInfo(folder.FullName) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            AddLog($"打开日志文件夹失败：{ex.Message}", pushWebhook: false);
        }
    }

    [RelayCommand]
    private async Task ExportLogsAsync()
    {
        if (LogEntries.Count == 0)
        {
            AddLog("没有可导出的日志。");
            return;
        }

        var logDir = RuntimeLogService.LogDirectory;
        Directory.CreateDirectory(logDir);

        var path = Path.Combine(logDir, $"runtime-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var content = string.Join(Environment.NewLine, LogEntries.Select(x => x.DisplayText));
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
        AddLog($"运行日志已导出：{path}");
    }

    [RelayCommand]
    private void TestWebhook()
    {
        _ = Task.Run(TestWebhookAsync);
    }

    private async Task TestWebhookAsync()
    {
        if (!WebhookEnabled || string.IsNullOrWhiteSpace(WebhookUrl))
        {
            AddLog("Webhook 未启用或 URL 为空。");
            return;
        }

        var testContent = "这是一条来自 MAAA 的测试消息。";
        var time = DateTime.Now.ToString("HH:mm:ss");
        var requestBody = WebhookBody
            .Replace("__TIME__", time)
            .Replace("__CONTENT__", testContent);

        AddLog($"测试内容已发送: {testContent}");
        AddLog($"Webhook 测试请求体:\n{requestBody}");
        await WebhookService.SendAsync(WebhookUrl, WebhookBody, time, testContent);
        AddLog("Webhook 测试请求已发送。");
    }
}
