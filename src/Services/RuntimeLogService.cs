using System.Diagnostics;
using System.IO;
using System.Text;

namespace Game_Daily_Routine_Launcher;

public static class RuntimeLogService
{
    private const int RetentionDays = 5;
    private const string LogFilePattern = "session-*.log";
    private const string FilePrefix = "session-";
    private const string TimestampFormat = "yyyyMMdd-HHmmss-fff";

    private static readonly object SyncRoot = new();
    private static string? _currentLogFilePath;

    internal static string LogDirectory { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

    public static string? CurrentLogFilePath => _currentLogFilePath;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_currentLogFilePath != null)
                return;

            try
            {
                var logDir = LogDirectory;
                Directory.CreateDirectory(logDir);

                _currentLogFilePath = CreateSessionLogPath(logDir);
                File.WriteAllText(
                    _currentLogFilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] 程序启动。{Environment.NewLine}",
                    Encoding.UTF8);

                PruneExpiredSessionLogs(logDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Runtime log initialize failed: {ex}");
            }
        }
    }

    public static void WriteEntry(LogEntryRecord entry)
    {
        var text = string.IsNullOrWhiteSpace(entry.RawBody)
            ? $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.Message}"
            : $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.Message}{Environment.NewLine}{entry.RawBody}";

        WriteText(text);
    }

    public static void WriteMessage(string message) =>
        WriteText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

    public static void WriteException(string message, Exception exception) =>
        WriteText($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}{exception}");

    private static void WriteText(string text)
    {
        lock (SyncRoot)
        {
            try
            {
                if (_currentLogFilePath == null)
                    Initialize();

                if (_currentLogFilePath == null)
                    return;

                File.AppendAllText(_currentLogFilePath, text + Environment.NewLine, Encoding.UTF8);
                var logDir = Path.GetDirectoryName(_currentLogFilePath);
                if (!string.IsNullOrWhiteSpace(logDir))
                    PruneExpiredSessionLogs(logDir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Runtime log write failed: {ex}");
            }
        }
    }

    private static string CreateSessionLogPath(string logDir)
    {
        var timestamp = DateTime.Now.ToString(TimestampFormat);
        var path = Path.Combine(logDir, $"{FilePrefix}{timestamp}.log");
        if (!File.Exists(path))
            return path;

        for (var index = 1; index < 1000; index++)
        {
            path = Path.Combine(logDir, $"{FilePrefix}{timestamp}-{index}.log");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(logDir, $"{FilePrefix}{timestamp}-{Guid.NewGuid():N}.log");
    }

    private static void PruneExpiredSessionLogs(string logDir)
    {
        var sessionLogs = Directory
            .GetFiles(logDir, LogFilePattern)
            .Select(path => (Path: path, Timestamp: GetLogTimestamp(path)))
            .ToList();

        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        var expiredLogs = sessionLogs
            .Where(x => x.Timestamp < cutoff)
            .OrderBy(x => x.Timestamp)
            .ToList();

        var currentPath = _currentLogFilePath == null ? string.Empty : Path.GetFullPath(_currentLogFilePath);
        foreach (var item in expiredLogs)
        {
            if (string.Equals(Path.GetFullPath(item.Path), currentPath, StringComparison.OrdinalIgnoreCase))
                continue;

            File.Delete(item.Path);
        }
    }

    private static DateTime GetLogTimestamp(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (fileName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase) &&
            fileName.Length >= FilePrefix.Length + TimestampFormat.Length)
        {
            var timestampText = fileName.Substring(FilePrefix.Length, TimestampFormat.Length);
            if (DateTime.TryParseExact(timestampText, TimestampFormat, null, System.Globalization.DateTimeStyles.None, out var timestamp))
                return timestamp;
        }

        return File.GetLastWriteTime(path);
    }
}
