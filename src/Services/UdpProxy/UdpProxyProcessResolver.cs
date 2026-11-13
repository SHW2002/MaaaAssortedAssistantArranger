namespace Game_Daily_Routine_Launcher;

public sealed record UdpProxyProcessSelection(string[] ProcessNames, string Source);

public static class UdpProxyProcessResolver
{
    public static UdpProxyProcessSelection Resolve(GameTaskConfig task)
    {
        if (!string.IsNullOrWhiteSpace(task.UdpProxy.ProcessNames))
            return new(SplitNames(task.UdpProxy.ProcessNames), "自定义覆盖");

        var monitor = task.GameProcessName?.Trim() ?? string.Empty;
        if (monitor.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) monitor = monitor[..^4];
        var preset = FindPreset(task.Id, task.Name, monitor);
        return preset switch
        {
            "march7th" => new(["StarRail.exe"], "自动预设 · 星穹铁道"),
            "zzz" => new(["ZenlessZoneZero.exe"], "自动预设 · 绝区零"),
            "genshin" => new([monitor.Equals("GenshinImpact", StringComparison.OrdinalIgnoreCase)
                ? "GenshinImpact.exe" : "YuanShen.exe"], "自动预设 · 原神"),
            "maaend" => new(["Endfield.exe"], "自动预设 · 终末地"),
            "ww" => new(["Client-Win64-Shipping.exe"], "自动预设 · 鸣潮"),
            _ => new(monitor.Length > 0 ? [monitor + ".exe"] : [], "自动跟随监控进程")
        };
    }

    private static string[] SplitNames(string value) => value.Split(['\r', '\n', ';', ','],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string FindPreset(string? id, string? name, string monitor)
    {
        var key = id?.ToLowerInvariant();
        if (key is "march7th" or "zzz" or "genshin" or "maaend" or "ww") return key;
        key = monitor.ToLowerInvariant() switch
        {
            "starrail" => "march7th",
            "zenlesszonezero" => "zzz",
            "yuanshen" or "genshinimpact" => "genshin",
            "endfield" => "maaend",
            "wuthering waves" => "ww",
            _ => string.Empty
        };
        if (key.Length > 0) return key;
        name ??= string.Empty;
        if (HasName(name, "星穹铁道", "Star Rail", "March7th")) return "march7th";
        if (HasName(name, "绝区零", "ZenlessZoneZero", "Zenless Zone Zero")) return "zzz";
        if (HasName(name, "原神", "Genshin", "BetterGI")) return "genshin";
        if (HasName(name, "终末地", "Endfield", "MaaEnd")) return "maaend";
        if (HasName(name, "鸣潮", "Wuthering Waves", "ok-ww")) return "ww";
        return string.Empty;
    }

    private static bool HasName(string name, params string[] aliases) =>
        aliases.Any(alias => name.Contains(alias, StringComparison.OrdinalIgnoreCase));
}
