using System.Text.Json.Serialization;

namespace Game_Daily_Routine_Launcher;

public enum LaunchMode
{
    File,
    Uri
}

public class GameTaskConfig
{
    internal const int MaxLogInactivityTimeoutMinutes = (int)((uint.MaxValue - 1L) / 60_000);
    internal static string LogInactivityTimeoutError => $"无日志超时必须为 1 至 {MaxLogInactivityTimeoutMinutes} 分钟。";

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ToolPath { get; set; } = string.Empty;
    public string ToolArgs { get; set; } = string.Empty;
    public string GameProcessName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LaunchMode LaunchMode { get; set; } = LaunchMode.File;

    public int DelayBeforeStartMs { get; set; }
    public int TimeoutMinutes { get; set; } = 60;
    public bool LogMonitoringEnabled { get; set; }
    public int LogInactivityTimeoutMinutes { get; set; } = 10;

    internal static bool IsValidLogInactivityTimeout(int minutes) => minutes is > 0 and <= MaxLogInactivityTimeoutMinutes;

    private UdpProxyConfig _udpProxy = new();
    public UdpProxyConfig UdpProxy
    {
        get => _udpProxy;
        set => _udpProxy = value ?? new();
    }
}
