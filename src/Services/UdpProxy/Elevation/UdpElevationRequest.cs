namespace Game_Daily_Routine_Launcher;

public sealed record UdpElevationRequest(
    string Operation, int ParentProcessId, int TaskIndex = -1, bool AutoSummaryRequested = false)
{
    public const string StartAll = "all";
    public const string StartSingle = "single";
    public const string Prepare = "prepare";
    private const string Argument = "--udp-resume";

    public IEnumerable<string> ToArguments() =>
        [Argument, Operation, ParentProcessId.ToString(), TaskIndex.ToString(), AutoSummaryRequested.ToString()];

    public static UdpElevationRequest? Parse(string[] arguments)
    {
        var index = Array.IndexOf(arguments, Argument);
        if (index < 0) return null;
        if (index + 5 != arguments.Length ||
            !int.TryParse(arguments[index + 2], out var parent) || parent <= 0 || parent == Environment.ProcessId ||
            !int.TryParse(arguments[index + 3], out var task) ||
            !bool.TryParse(arguments[index + 4], out var summary))
            throw new InvalidOperationException("UDP 提权恢复参数无效，请正常重新启动程序。");
        var operation = arguments[index + 1];
        if (operation is not (StartAll or StartSingle or Prepare) ||
            (operation == StartSingle ? task < 0 : task != -1))
            throw new InvalidOperationException("UDP 提权恢复操作无效，请正常重新启动程序。");
        return new(operation, parent, task, summary);
    }
}
