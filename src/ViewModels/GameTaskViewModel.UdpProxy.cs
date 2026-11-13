using CommunityToolkit.Mvvm.ComponentModel;

namespace Game_Daily_Routine_Launcher;

public partial class GameTaskViewModel
{
    [ObservableProperty] private bool _udpProxyEnabled;
    [ObservableProperty] private string _udpProxyProcessNames = string.Empty;
    [ObservableProperty] private string _udpProxyHost = "127.0.0.1";
    [ObservableProperty] private int _udpProxyPort = 7890;

    partial void OnUdpProxyEnabledChanged(bool value) => Validate();
    partial void OnUdpProxyProcessNamesChanged(string value) => Validate();
    partial void OnUdpProxyHostChanged(string value) => Validate();
    partial void OnUdpProxyPortChanged(int value) => Validate();
    partial void OnGameProcessNameChanged(string value) => Validate();
    partial void OnTimeoutMinutesChanged(int value) => Validate();

    private void ValidateUdpProxy()
    {
        try
        {
            UdpProxyRules.Validate(new GameTaskConfig
            {
                GameProcessName = GameProcessName, TimeoutMinutes = TimeoutMinutes,
                UdpProxy = BuildUdpProxyConfig()
            });
        }
        catch (InvalidOperationException ex) { SetValidation(ex.Message); }
    }

    private void LoadUdpProxy(UdpProxyConfig? config)
    {
        config ??= new();
        UdpProxyEnabled = config.Enabled;
        UdpProxyProcessNames = config.ProcessNames;
        UdpProxyHost = config.Socks5Host;
        UdpProxyPort = config.Socks5Port;
    }

    private UdpProxyConfig BuildUdpProxyConfig() => new()
    {
        Enabled = UdpProxyEnabled,
        ProcessNames = UdpProxyProcessNames,
        Socks5Host = UdpProxyHost,
        Socks5Port = UdpProxyPort
    };
}
