namespace Game_Daily_Routine_Launcher;

public enum ThemeMode { System, Light, Dark }

public sealed record ThemeOption(ThemeMode Mode, string Label)
{
    public static IReadOnlyList<ThemeOption> All { get; } =
    [new(ThemeMode.System, "跟随系统"), new(ThemeMode.Light, "浅色"), new(ThemeMode.Dark, "深色")];
}
