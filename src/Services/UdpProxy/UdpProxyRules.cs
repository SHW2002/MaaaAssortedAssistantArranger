using System.IO;
using System.Net;
using System.Globalization;

namespace Game_Daily_Routine_Launcher;

public static class UdpProxyRules
{
    public static string[] Validate(GameTaskConfig task)
    {
        var config = task.UdpProxy;
        if (!config.Enabled) return [];
        if (string.IsNullOrWhiteSpace(task.GameProcessName) ||
            task.GameProcessName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            task.GameProcessName.IndexOfAny([';', ',']) >= 0 ||
            task.GameProcessName.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("启用 UDP 代理时，必须配置有效的监控进程名。");
        if (task.TimeoutMinutes <= 0)
            throw new InvalidOperationException("启用 UDP 代理时，超时必须大于 0 分钟。");
        var names = (config.ProcessNames ?? string.Empty).Split(['\r', '\n', ';', ','],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0 || names.Any(name => name.Length > 255 ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            name.Length <= 4 || name != name.Trim()))
            throw new InvalidOperationException("UDP 代理进程请填写完整文件名（如 StarRail.exe），以逗号、分号或换行分隔，不支持目录或通配符。");
        if (names.Any(name => name.Equals("ProxiFyre.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("MaaaUdpProxy.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("MaaaAssortedAssistantArranger.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("不能将代理引擎或本程序配置为代理目标。");
        if (Uri.CheckHostName(config.Socks5Host) is not (UriHostNameType.IPv4 or UriHostNameType.Dns) ||
            config.Socks5Port is < 1 or > 65535)
            throw new InvalidOperationException("请填写有效的 SOCKS5 IPv4 地址或主机名及端口（1–65535）。");
        return names;
    }

    public static bool UsesLocalUpstream(UdpProxyConfig config) =>
        string.Equals(config.Socks5Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(config.Socks5Host, out var address) && IPAddress.IsLoopback(address);

    public static void ValidateUpstreamCompatibility(IReadOnlyList<GameTaskConfig> tasks, bool closeFlClash)
    {
        if (closeFlClash && tasks.Any(task => task.Enabled && task.UdpProxy.Enabled && UsesLocalUpstream(task.UdpProxy)))
            throw new InvalidOperationException("任务使用本机 SOCKS5 UDP 上游，请先取消‘运行前关闭 FlClash’，以免关闭上游代理。");
    }

    public static IReadOnlyList<string> BuildArguments(GameTaskConfig task)
    {
        var names = Validate(task);
        if (!task.UdpProxy.Enabled)
            throw new InvalidOperationException("UDP 代理尚未启用。");
        var host = new IdnMapping().GetAscii(task.UdpProxy.Socks5Host);
        var arguments = new List<string> { "--endpoint", $"{host}:{task.UdpProxy.Socks5Port}" };
        foreach (var name in names)
        {
            arguments.Add("--app");
            arguments.Add(name);
        }
        return arguments;
    }
}
