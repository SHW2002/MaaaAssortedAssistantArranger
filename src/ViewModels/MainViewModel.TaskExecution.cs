using CommunityToolkit.Mvvm.Input;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    [RelayCommand(CanExecute = nameof(CanStartAll))]
    private async Task StartAllAsync()
    {
        SaveConfig();
        var enabledTasks = Tasks.Where(t => t.Enabled).Select(t => t.ToConfig()).ToList();
        if (enabledTasks.Count == 0)
        {
            AddLog("没有启用的任务。");
            IsRunning = false;
            return;
        }

        if (UdpElevationService.NeedsElevation(enabledTasks, UdpProxyPaths.IsAdministrator) &&
            RequestUdpElevation(UdpElevationRequest.StartAll))
            return;
        IsRunning = true;

        var shouldMuteOnStart = MuteOnStart && (!MuteOnlyOnAutoRun || App.IsAutoRun);
        if (shouldMuteOnStart)
        {
            if (!_hasMuteSnapshot)
                CaptureMuteSnapshot();
            _didAutoMute = true;
        }

        foreach (var task in Tasks)
            task.State = TaskState.Idle;

        var runner = CreateTaskChainRunner();
        _chainRunner = runner;
        runner.LogMessage += msg => AddLog(msg);
        runner.TaskStateChanged += (id, state) =>
        {
            ExecuteOnUiThread(() =>
            {
                var vm = Tasks.FirstOrDefault(t => t.Id == id);
                if (vm != null && Enum.TryParse<TaskState>(state, out var parsed))
                    vm.State = parsed;
            });
        };

        var screenRecordingStarted = TryStartScreenRecording();
        AddLog("开始执行任务链...");
        var autoSummaryRequested = _autoSummaryRequested;
        _autoSummaryRequested = false;

        var completed = false;
        var reachedEnd = false;
        try
        {
            completed = await runner.RunChainAsync(enabledTasks, shouldMuteOnStart);
            reachedEnd = runner.ReachedEnd;
        }
        finally
        {
            if (screenRecordingStarted)
                await StopScreenRecordingAsync();
            runner.Dispose();
            _chainRunner = null;
            IsRunning = false;
        }

        if (RestoreVolumeOnCompletion || RestoreBrightnessOnCompletion)
            await RestoreConfiguredStateAsync();

        await PublishChainResultAsync(reachedEnd, completed, autoSummaryRequested);

        if (completed && ShutdownOnComplete && (!ShutdownOnlyOnAutoRun || autoSummaryRequested))
        {
            AddLog("所有任务已完成，10秒后关机。");
            SystemService.Shutdown();
        }
    }

    private async Task PublishChainResultAsync(bool reachedEnd, bool completed, bool autoSummaryRequested)
    {
        if (!reachedEnd)
        {
            AddLog("任务链已停止。");
            return;
        }

        var resultPush = AppendLog(CreateLogEntry(completed ? "任务链执行完成。" : "任务链执行结束，部分任务未成功。"));
        if (AiSummaryEnabled)
            await GenerateAndLogAiSummaryAsync(autoSummaryRequested);
        else if (WebhookOnlyPushAiSummary)
            await PushFinalAiSummaryWebhookAsync("未开启AI总结服务", null);

        await resultPush;
    }

    [RelayCommand(CanExecute = nameof(CanStopAll))]
    private void StopAll()
    {
        _chainRunner?.Stop();

        foreach (var task in Tasks)
            task.State = TaskState.Idle;

        AddLog("正在停止所有任务...");
    }

    [RelayCommand(CanExecute = nameof(CanStartAll))]
    private async Task StartSingleAsync(GameTaskViewModel? taskVm)
    {
        if (taskVm == null)
            return;

        SaveConfig();
        if (UdpElevationService.NeedsElevation([taskVm.ToConfig()], UdpProxyPaths.IsAdministrator) &&
            RequestUdpElevation(UdpElevationRequest.StartSingle, Tasks.IndexOf(taskVm)))
            return;
        IsRunning = true;
        taskVm.State = TaskState.Running;
        AddLog($"单独启动 {taskVm.Name}...");

        var runner = CreateTaskChainRunner();
        _chainRunner = runner;
        runner.LogMessage += msg => AddLog(msg);
        runner.TaskStateChanged += (id, state) =>
        {
            ExecuteOnUiThread(() =>
            {
                if (Enum.TryParse<TaskState>(state, out var parsed))
                    taskVm.State = parsed;
            });
        };

        var screenRecordingStarted = false;
        try
        {
            screenRecordingStarted = TryStartScreenRecording();
            await runner.RunChainAsync([taskVm.ToConfig()], false);
        }
        finally
        {
            if (screenRecordingStarted)
                await StopScreenRecordingAsync();
            runner.Dispose();
            _chainRunner = null;
            IsRunning = false;
        }

        if (runner.ReachedEnd && taskVm.State is TaskState.TimedOut or TaskState.Terminated)
            await PublishChainResultAsync(reachedEnd: true, completed: false, autoSummaryRequested: false);
    }
}
