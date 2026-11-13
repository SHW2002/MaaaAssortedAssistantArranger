using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Game_Daily_Routine_Launcher;

internal static class UdpProxyDriver
{
    public static bool HasDriver()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\NDISRD");
        var path = Path.Combine(Environment.SystemDirectory, "drivers", "ndisrd.sys");
        if (key is null || !File.Exists(path)) return false;
        var version = FileVersionInfo.GetVersionInfo(path);
        if (version.FileMajorPart != 3 || new Version(version.FileMajorPart, version.FileMinorPart,
                version.FileBuildPart, version.FilePrivatePart) < new Version(3, 6, 1, 0))
            throw new InvalidOperationException("已有 Windows Packet Filter 版本不兼容，需要 3.6.1 至 3.x；本工具不会覆盖它。");
        return true;
    }

    public static async Task InstallAsync(string msiPath)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var arg in new[] { "/i", msiPath, "/qn", "/norestart", "REBOOT=ReallySuppress" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("无法启动驱动安装程序。");
        // Finish an MSI transaction before allowing the application to exit.
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode is 3010 or 1641)
            throw new InvalidOperationException("驱动安装完成，需要重启 Windows 后再使用 UDP 代理；本程序不会自动重启电脑。");
        if (process.ExitCode != 0)
            throw new IOException($"Windows Packet Filter 安装失败，退出码 {process.ExitCode}。");
    }
}
