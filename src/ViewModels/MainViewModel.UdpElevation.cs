using System.Windows;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    private bool RequestUdpElevation(string operation, int taskIndex = -1)
    {
        if (UdpProxyPaths.IsAdministrator) return false;
        _hasRunInCurrentWindow = true;
        IsPreparingUdpProxy = true;
        AddLog("UDP 代理需要管理员权限，正在申请；同意 UAC 提示后将重新启动并继续本次操作。");
        var request = new UdpElevationRequest(operation, Environment.ProcessId, taskIndex, _autoSummaryRequested);
        var (success, message) = UdpElevationService.TryStart(request, App.IsAutoRun);
        AddLog(message);
        if (success)
            _ = Application.Current.Dispatcher.BeginInvoke(new Action(() => Application.Current.MainWindow.Close()));
        else
        {
            IsPreparingUdpProxy = false;
            _autoSummaryRequested = false;
        }
        return true;
    }

    public async Task ResumeUdpOperationAsync(UdpElevationRequest request)
    {
        // 本次操作已经由旧实例确认，不再触发自动调度或重复询问驱动安装。
        _hasRunInCurrentWindow = true;
        _autoSummaryRequested = request.AutoSummaryRequested;
        AddLog("已以管理员权限重新启动，继续提权前的操作。");
        switch (request.Operation)
        {
            case UdpElevationRequest.StartAll:
                await StartAllCommand.ExecuteAsync(null);
                break;
            case UdpElevationRequest.StartSingle:
                if (request.TaskIndex >= Tasks.Count)
                    throw new InvalidOperationException("原任务已不存在，无法恢复单任务启动。");
                await StartSingleCommand.ExecuteAsync(Tasks[request.TaskIndex]);
                break;
            case UdpElevationRequest.Prepare:
                await PrepareUdpProxyCoreAsync();
                break;
        }
    }
}
