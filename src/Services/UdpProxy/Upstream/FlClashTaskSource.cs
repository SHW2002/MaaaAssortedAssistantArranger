using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;

namespace Game_Daily_Routine_Launcher;

internal static class FlClashTaskSource
{
    public static FlClashTaskRoute? TryRead(UdpProxyConfig endpoint, int isolatedPort)
    {
        if (!UdpProxyRules.UsesLocalUpstream(endpoint)) return null;
        var processes = Process.GetProcessesByName("FlClash");
        try { if (processes.Length == 0) return null; }
        finally { foreach (var process in processes) process.Dispose(); }
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "com.follow", "clash");
        var configPath = Path.Combine(directory, "config.yaml");
        var preferencesPath = Path.Combine(directory, "shared_preferences.json");
        if (!File.Exists(configPath) || !File.Exists(preferencesPath)) return null;
        if (new FileInfo(configPath).Length > 32 * 1024 * 1024)
            throw new IOException("FlClash 配置文件过大，已拒绝自动导入。");
        var source = FlClashTaskRouteBuilder.ParseYaml(File.ReadAllText(configPath));
        if (source["mixed-port"]?.ToString() != endpoint.Socks5Port.ToString() &&
            source["socks-port"]?.ToString() != endpoint.Socks5Port.ToString()) return null;
        var shared = JsonNode.Parse(File.ReadAllText(preferencesPath))!.AsObject();
        var preferences = JsonNode.Parse(shared["flutter.config"]!.GetValue<string>())!.AsObject();
        return FlClashTaskRouteBuilder.Build(source, preferences, isolatedPort);
    }
}
