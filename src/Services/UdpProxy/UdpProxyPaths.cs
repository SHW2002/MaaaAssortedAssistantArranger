using System.IO;
using System.Security.Principal;

namespace Game_Daily_Routine_Launcher;

public static class UdpProxyPaths
{
    public static string EngineDirectory => Path.Combine(AppContext.BaseDirectory, "components", "udp");
    public static string EngineExecutable => Path.Combine(EngineDirectory, "MaaaUdpProxy.exe");
    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static void RequireAdministrator()
    {
        if (!IsAdministrator)
            throw new InvalidOperationException("UDP 代理需要管理员权限，请以管理员身份运行本程序；无人值守运行需在任务计划程序中勾选‘使用最高权限运行’。");
    }
}
