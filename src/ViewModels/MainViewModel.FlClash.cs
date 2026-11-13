namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private readonly FlClashService _flClashService;
    private Task? _startupFlClashTask;

    private async Task CloseStartupFlClashAsync()
    {
        try { await _flClashService.CloseAsync(CancellationToken.None); }
        catch (Exception ex) { AddLog($"启动时关闭 FlClash 失败：{ex.Message}"); }
    }

    private TaskChainRunner CreateTaskChainRunner()
    {
        var closeFlClash = CloseFlClashBeforeRun;
        return new TaskChainRunner(_audioService, PollIntervalSeconds * 1000)
        {
            ValidateTasks = tasks => UdpProxyRules.ValidateUpstreamCompatibility(tasks, closeFlClash),
            BeforeTaskAsync = async ct =>
            {
                if (_startupFlClashTask is not null)
                    await _startupFlClashTask;
                ct.ThrowIfCancellationRequested();
                if (closeFlClash)
                {
                    try { await _flClashService.CloseAsync(ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        AddLog($"关闭 FlClash 失败，已中止任务：{ex.Message}");
                        throw;
                    }
                }
            },
            AfterChainAsync = () => _flClashService.RestoreAsync()
        };
    }
}
