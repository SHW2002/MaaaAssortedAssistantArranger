using System.IO;
using System.Text;

namespace Game_Daily_Routine_Launcher;

public sealed class AiPromptLogService
{
    private const int RetentionDays = 5;
    private const string FilePrefix = "aiprompt-";
    private const string LegacyFileName = "aiprompt.log";
    private const string TimestampFormat = "yyyyMMdd-HHmmss-fff";
    private readonly string _logDir = RuntimeLogService.LogDirectory;

    public async Task WriteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        Directory.CreateDirectory(_logDir);

        var filePath = CreateLogFilePath();

        var builder = new StringBuilder();
        builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]");
        builder.AppendLine(prompt.TrimEnd());

        await File.WriteAllTextAsync(filePath, builder.ToString(), Encoding.UTF8, cancellationToken);
        PruneExpiredPromptLogs();
    }

    public bool HasAvailablePromptLog() => GetLatestPromptLogPath() != null;

    public async Task<string?> ReadLatestPromptAsync(CancellationToken cancellationToken = default)
    {
        var filePath = GetLatestPromptLogPath();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ExtractPromptContent(content);
    }

    private string CreateLogFilePath()
    {
        var timestamp = DateTime.Now.ToString(TimestampFormat);
        var path = Path.Combine(_logDir, $"{FilePrefix}{timestamp}.log");
        if (!File.Exists(path))
            return path;

        for (var index = 1; index < 1000; index++)
        {
            path = Path.Combine(_logDir, $"{FilePrefix}{timestamp}-{index}.log");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(_logDir, $"{FilePrefix}{timestamp}-{Guid.NewGuid():N}.log");
    }

    private string? GetLatestPromptLogPath()
    {
        if (!Directory.Exists(_logDir))
            return null;

        return Directory.EnumerateFiles(_logDir, $"{FilePrefix}*.log")
            .Concat(Directory.EnumerateFiles(_logDir, LegacyFileName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(GetLogTimestamp)
            .FirstOrDefault();
    }

    private void PruneExpiredPromptLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            var files = Directory.EnumerateFiles(_logDir, $"{FilePrefix}*.log")
                .Concat(Directory.EnumerateFiles(_logDir, LegacyFileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => (Path: path, Timestamp: GetLogTimestamp(path)))
                .Where(x => x.Timestamp < cutoff)
                .ToList();

            foreach (var item in files)
                File.Delete(item.Path);
        }
        catch (Exception ex)
        {
            RuntimeLogService.WriteException("AI Prompt 日志清理失败", ex);
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

    private static string? ExtractPromptContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        using var reader = new StringReader(content);
        var firstLine = reader.ReadLine();
        var remaining = reader.ReadToEnd().Trim();

        if (!string.IsNullOrWhiteSpace(remaining) &&
            !string.IsNullOrWhiteSpace(firstLine) &&
            firstLine.StartsWith('[') &&
            firstLine.EndsWith(']'))
        {
            return remaining;
        }

        return content.Trim();
    }
}
