using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Game_Daily_Routine_Launcher;

public static class SystemService
{
    private const string LegacyRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyTaskPrefix = "GameDailyRoutineLauncher";
    private const string LegacyBatchTaskName = "RunMyBatchAtLogon";

    public static void Shutdown(int delaySeconds = 10)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = $"-s -t {delaySeconds}",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    public static void CancelShutdown()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = "-a",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    public static string GetAutoRunTaskName()
    {
        var exePath = GetCurrentExePath() ?? "unknown";
        var normalizedPath = NormalizePath(exePath);
        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        return $"{GetTaskDisplayName(fileName)}-{BuildShortHash(normalizedPath)}";
    }

    public static (bool success, string message) RegisterAutoRun() => RegisterAutoRun(GetAutoRunTaskName());

    public static (bool success, string message) RegisterAutoRun(string taskName)
    {
        var exePath = GetCurrentExePath();
        if (exePath == null)
            return (false, "无法获取当前程序路径。");

        var command = $"\"{exePath}\" --autorun";
        var createResult = RunSchtasksWithAutoElevate(
            "/Create",
            "/TN", taskName,
            "/TR", command,
            "/SC", "ONLOGON",
            "/RL", "HIGHEST",
            "/F");

        if (!createResult.success)
            return createResult;

        var cleanupMessages = CleanupLegacyAutoRunEntries().ToList();
        cleanupMessages.AddRange(CleanupLegacyScheduledTasks(taskName, exePath));
        var cleanupText = FormatCleanupMessages(cleanupMessages);
        return (true, $"已创建计划任务 {taskName}，登录时将自动启动。{cleanupText}");
    }

    public static (bool success, string message) UnregisterAutoRun() => UnregisterAutoRun(GetAutoRunTaskName());

    public static (bool success, string message) UnregisterAutoRun(string taskName)
    {
        var exePath = GetCurrentExePath();
        var deleteResult = DeleteScheduledTask(taskName);
        if (!deleteResult.success)
            return deleteResult;

        var cleanupMessages = CleanupLegacyAutoRunEntries().ToList();
        if (exePath != null)
            cleanupMessages.AddRange(CleanupLegacyScheduledTasks(taskName, exePath));

        var cleanupText = FormatCleanupMessages(cleanupMessages);
        return (true, $"{deleteResult.message}{cleanupText}");
    }

    public static bool IsAutoRunRegistered() => IsAutoRunRegistered(GetAutoRunTaskName());

    public static bool IsAutoRunRegistered(string taskName)
    {
        return IsScheduledTaskRegistered(taskName) || HasLegacyAutoRunEntryForCurrentInstance();
    }

    public static (bool changed, string? message) EnsureAutoRunUsesScheduledTask() => EnsureAutoRunUsesScheduledTask(GetAutoRunTaskName());

    public static (bool changed, string? message) EnsureAutoRunUsesScheduledTask(string taskName)
    {
        var exePath = GetCurrentExePath();
        if (exePath == null)
            return (false, null);

        var messages = new List<string>();
        var hasLegacyRegistry = HasLegacyAutoRunEntryForCurrentInstance();
        var legacyTasks = GetLegacyScheduledTaskNames(taskName)
            .Where(name => IsScheduledTaskForExe(name, exePath))
            .ToList();

        if ((hasLegacyRegistry || legacyTasks.Count > 0) && !IsScheduledTaskRegistered(taskName))
        {
            var migrated = RegisterAutoRun(taskName);
            if (!migrated.success)
                messages.Add($"检测到旧版开机自启项，但迁移失败：{migrated.message}");
            else
                messages.Add($"检测到旧版开机自启项，已迁移为当前实例专属计划任务 {taskName}。");
        }

        messages.AddRange(CleanupLegacyAutoRunEntries());
        messages.AddRange(CleanupLegacyScheduledTasks(taskName, exePath));

        return messages.Count > 0
            ? (true, string.Join(" ", messages))
            : (false, null);
    }

    private static bool IsScheduledTaskRegistered(string taskName)
    {
        var result = RunSchtasks("/Query", "/TN", taskName);
        return result.success;
    }

    private static (bool success, string message) DeleteScheduledTask(string taskName)
    {
        if (!IsScheduledTaskRegistered(taskName))
            return (true, $"计划任务 {taskName} 不存在，无需删除。");

        var deleteResult = RunSchtasksWithAutoElevate("/Delete", "/TN", taskName, "/F");
        return deleteResult.success
            ? (true, $"已删除计划任务 {taskName}。")
            : deleteResult;
    }

    private static (bool success, string message) RunSchtasksWithAutoElevate(params string[] arguments)
    {
        var result = RunProcess("schtasks.exe", arguments);
        if (result.success)
            return result;

        if (!IsAccessDenied(result.message))
            return result;

        var elevatedResult = RunElevatedProcess("schtasks.exe", arguments);
        if (elevatedResult.success)
            return elevatedResult;

        return (false, AppendElevateHint(elevatedResult.message));
    }

    private static (bool success, string message) RunSchtasks(params string[] arguments) => RunProcess("schtasks.exe", arguments);

    private static (bool success, string message) RunProcess(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (false, $"无法启动 {fileName}。");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var message = NormalizeMessage(output, error);
            return process.ExitCode == 0
                ? (true, message)
                : (false, message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool success, string message) RunElevatedProcess(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (false, $"无法以管理员权限启动 {fileName}。");

            process.WaitForExit();
            return process.ExitCode == 0
                ? (true, "已自动获取管理员权限并执行。")
                : (false, $"以管理员权限执行失败，退出码 {process.ExitCode}。");
        }
        catch (Win32Exception ex) when ((uint)ex.NativeErrorCode == 1223)
        {
            return (false, "用户取消了管理员权限请求。");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static bool IsAccessDenied(string message)
    {
        return message.Contains("拒绝访问", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase);
    }

    private static string AppendElevateHint(string message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "执行失败，已尝试自动获取管理员权限。"
            : $"{message} 已尝试自动获取管理员权限。";
    }

    private static string NormalizeMessage(string output, string error)
    {
        var parts = new[] { output, error }.Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Trim());
        var message = string.Join(Environment.NewLine, parts);
        return string.IsNullOrWhiteSpace(message) ? "命令执行成功。" : message;
    }

    private static string? GetCurrentExePath()
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        return string.IsNullOrWhiteSpace(exePath) ? null : exePath;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        }
        catch
        {
            return path.Trim().ToUpperInvariant();
        }
    }

    private static string BuildShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..12];
    }

    private static string GetTaskDisplayName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "UnknownApp";

        return string.Equals(fileName, "MaaaAssortedAssistantArranger", StringComparison.OrdinalIgnoreCase)
            ? "MAAA"
            : fileName;
    }

    private static string GetLegacyFileNameTaskName()
    {
        var exePath = GetCurrentExePath() ?? "unknown";
        var fileName = Path.GetFileNameWithoutExtension(exePath);
        var normalized = string.IsNullOrWhiteSpace(fileName) ? "UnknownApp" : fileName;
        return $"{LegacyTaskPrefix}-{normalized}";
    }

    private static IEnumerable<string> GetLegacyScheduledTaskNames(string currentTaskName)
    {
        return new[] { LegacyTaskPrefix, GetLegacyFileNameTaskName(), LegacyBatchTaskName }
            .Where(name => !string.Equals(name, currentTaskName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasLegacyAutoRunEntryForCurrentInstance()
    {
        return FindLegacyAutoRunEntriesForCurrentInstance().Count > 0;
    }

    private static IReadOnlyList<string> FindLegacyAutoRunEntriesForCurrentInstance()
    {
        var exePath = GetCurrentExePath();
        if (exePath == null)
            return Array.Empty<string>();

        var legacyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LegacyTaskPrefix,
            GetLegacyFileNameTaskName(),
            GetAutoRunTaskName()
        };

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey);
            if (key == null)
                return Array.Empty<string>();

            return key.GetValueNames()
                .Where(name => legacyNames.Contains(name) || CommandTargetsExe(key.GetValue(name) as string, exePath))
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> CleanupLegacyAutoRunEntries()
    {
        var names = FindLegacyAutoRunEntriesForCurrentInstance();
        if (names.Count == 0)
            return Array.Empty<string>();

        var messages = new List<string>();

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, writable: true);
            if (key == null)
                return messages;

            foreach (var name in names)
            {
                if (key.GetValue(name) == null)
                    continue;

                key.DeleteValue(name);
                messages.Add($"检测到旧版注册表自启项 {name}，已自动清理。");
            }
        }
        catch (Exception ex)
        {
            messages.Add($"检测到旧版注册表自启项，但清理失败：{ex.Message}");
        }

        return messages;
    }

    private static IEnumerable<string> CleanupLegacyScheduledTasks(string currentTaskName, string exePath)
    {
        foreach (var taskName in GetLegacyScheduledTaskNames(currentTaskName))
        {
            if (!IsScheduledTaskForExe(taskName, exePath))
                continue;

            var result = DeleteScheduledTask(taskName);
            yield return result.success
                ? $"检测到旧版共享计划任务 {taskName}，已自动清理。"
                : $"检测到旧版共享计划任务 {taskName}，但清理失败：{result.message}";
        }
    }

    private static bool IsScheduledTaskForExe(string taskName, string exePath)
    {
        var result = RunSchtasks("/Query", "/TN", taskName, "/XML");
        if (!result.success)
            return false;

        try
        {
            var document = XDocument.Parse(result.message);
            var exec = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Exec");
            var command = exec?.Elements().FirstOrDefault(node => node.Name.LocalName == "Command")?.Value;
            var arguments = exec?.Elements().FirstOrDefault(node => node.Name.LocalName == "Arguments")?.Value;
            return PathTargetsExe(command, exePath)
                || CommandTargetsExe($"{command} {arguments}", exePath);
        }
        catch
        {
            return CommandTargetsExe(result.message, exePath);
        }
    }

    private static bool CommandTargetsExe(string? command, string exePath)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        return command.Contains(exePath, StringComparison.OrdinalIgnoreCase)
            || command.Contains(NormalizePath(exePath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathTargetsExe(string? path, string exePath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return string.Equals(NormalizePath(path), NormalizePath(exePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatCleanupMessages(IReadOnlyCollection<string> messages)
    {
        return messages.Count == 0 ? string.Empty : $" {string.Join(" ", messages)}";
    }
}
