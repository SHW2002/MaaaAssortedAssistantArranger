namespace Game_Daily_Routine_Launcher;

public sealed class UdpProxyConfig
{
    public bool Enabled { get; set; }
    public string ProcessNames { get; set; } = string.Empty;
    public string Socks5Host { get; set; } = "127.0.0.1";
    public int Socks5Port { get; set; } = 7890;
}
