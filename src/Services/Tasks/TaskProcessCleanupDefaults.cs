namespace Game_Daily_Routine_Launcher;

internal static class TaskProcessCleanupDefaults
{
    internal static bool Apply(IEnumerable<GameTaskConfig> tasks)
    {
        var changed = false;
        foreach (var task in tasks.Where(task => task.RelatedProcessNames is null))
        {
            var processes = task.Id?.ToLowerInvariant() switch
            {
                "maa" => "MuMuNxMain.exe, MuMuNxDevice.exe",
                "march7th" => "StarRail.exe",
                "zzz" => "ZenlessZoneZero.exe",
                "genshin" => GenshinCleanupProcesses(task),
                "maaend" => "Endfield.exe",
                "ww" => "Wuthering Waves.exe, Client-Win64-Shipping.exe",
                _ => null
            };
            if (processes is null) continue;
            task.RelatedProcessNames = processes;
            changed = true;
        }
        return changed;
    }

    private static string GenshinCleanupProcesses(GameTaskConfig task)
    {
        var monitor = TaskProcessNames.WithoutExeExtension(task.GameProcessName);
        var game = monitor.Equals("GenshinImpact", StringComparison.OrdinalIgnoreCase)
            ? "GenshinImpact.exe" : "YuanShen.exe";
        return $"BetterGI.exe, {game}";
    }
}
