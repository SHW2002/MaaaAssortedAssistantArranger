using CommunityToolkit.Mvvm.ComponentModel;

namespace Game_Daily_Routine_Launcher;

public partial class GameTaskViewModel
{
    [ObservableProperty] private bool _udpProxyEnabled;
    [ObservableProperty] private string _udpProxyProcessNames = string.Empty;
    [ObservableProperty] private string _udpProxyHost = "127.0.0.1";
    [ObservableProperty] private int _udpProxyPort = 7890;

    partial void OnUdpProxyEnabledChanged(bool value) => Validate();
    public string UdpProxyProcessPreview
    {
        get
        {
            var selection = UdpProxyProcessResolver.Resolve(BuildUdpProxyTask());
            return selection.ProcessNames.Length == 0
                ? "请先配置监控进程，或填写自定义进程"
                : $"{selection.Source}：{string.Join(", ", selection.ProcessNames)}";
        }
    }

    partial void OnUdpProxyProcessNamesChanged(string value) => RefreshUdpProxyProcesses();
    partial void OnUdpProxyHostChanged(string value) => Validate();
    partial void OnUdpProxyPortChanged(int value) => Validate();
    partial void OnGameProcessNameChanged(string value) => RefreshUdpProxyProcesses();
    partial void OnNameChanged(string value) => RefreshUdpProxyProcesses();
    partial void OnTimeoutMinutesChanged(int value) => Validate();

    private void ValidateUdpProxy()
    {
        try
        {
            UdpProxyRules.Validate(BuildUdpProxyTask());
        }
        catch (InvalidOperationException ex) { SetValidation(ex.Message); }
    }

    private void LoadUdpProxy(UdpProxyConfig? config)
    {
        config ??= new();
        UdpProxyEnabled = config.Enabled;
        UdpProxyProcessNames = config.ProcessNames ?? string.Empty;
        UdpProxyHost = config.Socks5Host;
        UdpProxyPort = config.Socks5Port;
    }

    private void RefreshUdpProxyProcesses()
    {
        Validate();
        OnPropertyChanged(nameof(UdpProxyProcessPreview));
    }

    private GameTaskConfig BuildUdpProxyTask() => new()
    {
        Id = Id, Name = Name, ToolPath = ToolPath,
        GameProcessName = GameProcessName, TimeoutMinutes = TimeoutMinutes,
        UdpProxy = BuildUdpProxyConfig()
    };

    private UdpProxyConfig BuildUdpProxyConfig() => new()
    {
        Enabled = UdpProxyEnabled,
        ProcessNames = UdpProxyProcessNames,
        Socks5Host = UdpProxyHost,
        Socks5Port = UdpProxyPort
    };
}
