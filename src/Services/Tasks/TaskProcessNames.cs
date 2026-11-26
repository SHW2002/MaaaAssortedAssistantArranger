namespace Game_Daily_Routine_Launcher;

internal static class TaskProcessNames
{
    internal static string WithoutExeExtension(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    internal static string[] ForCleanup(string? value) => (value ?? string.Empty)
        .Split(['\r', '\n', ';', ',', '；', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(WithoutExeExtension)
        .Where(name => name.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
