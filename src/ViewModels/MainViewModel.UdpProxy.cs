using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    [ObservableProperty] private bool _isPreparingUdpProxy;
    private Task? _udpPreparationTask;
    private bool CanPrepareUdpProxy() => !IsRunning && !IsPreparingUdpProxy && !_isCleaningUp;

    [RelayCommand(CanExecute = nameof(CanPrepareUdpProxy))]
    private async Task PrepareUdpProxyAsync()
    {
        if (!UdpProxyPaths.IsAdministrator)
        {
            MessageBox.Show("请先退出本程序，并通过右键菜单‘以管理员身份运行’重新打开，然后准备 UDP 组件。",
                "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show("驱动及 UDP 转发组件已随包提供。启动任务会自动准备，也可在此提前安装经过 SHA-256 校验的官方 Windows Packet Filter 网络驱动；缺少安装包时自动下载。" +
                "\n\n安装驱动可能短暂重连网络，需要重启时会提示。驱动为共享系统组件，任务结束后保留；UDP 转发引擎仅随任务运行。" +
                "\n\n继续准备组件？", "准备 UDP 组件", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;
        IsPreparingUdpProxy = true;
        try
        {
            _udpPreparationTask = UdpProxySetup.PrepareAsync(message => AddLog(message), CancellationToken.None);
            await _udpPreparationTask;
        }
        catch (Exception ex)
        {
            AddLog($"准备 UDP 组件失败：{ex.Message}");
            MessageBox.Show(ex.Message, "准备 UDP 组件", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { IsPreparingUdpProxy = false; }
    }

    partial void OnIsPreparingUdpProxyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditTasks));
        StartAllCommand.NotifyCanExecuteChanged();
        StartSingleCommand.NotifyCanExecuteChanged();
        PrepareUdpProxyCommand.NotifyCanExecuteChanged();
    }
}
