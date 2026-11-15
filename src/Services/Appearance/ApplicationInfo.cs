using System.Reflection;

namespace Game_Daily_Routine_Launcher;

public static class ApplicationInfo
{
    public static string Version { get; } = typeof(App).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(App).Assembly.GetName().Version?.ToString(3) ?? "未知版本";

    public static string WindowTitle => $"Maaa Assorted Assistant Arranger v{Version}";
}
