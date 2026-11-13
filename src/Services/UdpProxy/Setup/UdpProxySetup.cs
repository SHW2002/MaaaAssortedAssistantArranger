using System.IO;
using System.Runtime.InteropServices;

namespace Game_Daily_Routine_Launcher;

public static class UdpProxySetup
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private const string DriverHash = "9c388c0b7f189f7fa98720bae2caecf7d64f30910838b80b438ecf8956b8502c";

    public static async Task PrepareAsync(Action<string> log, CancellationToken cancellationToken)
    {
        UdpProxyPaths.RequireAdministrator();
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("当前 UDP 组件仅支持原生 Windows x64。");
        if (!File.Exists(UdpProxyPaths.EngineExecutable))
            throw new FileNotFoundException("内置 UDP 组件缺失，请重新解压完整 MAAA 发布包。");
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (UdpProxyDriver.HasDriver())
            {
                log("兼容的 Windows Packet Filter 驱动已安装，无需重复准备。");
                return;
            }
            using var directory = new PrivateInstallDirectory();
            log("正在下载官方 Windows Packet Filter 3.6.2.1 x64 驱动...");
            using var driver = await VerifiedDownload.GetAsync(
                "https://github.com/wiresock/ndisapi/releases/download/v3.6.2/Windows.Packet.Filter.3.6.2.1.x64.msi",
                DriverHash, directory.Path, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            log("驱动 SHA-256 校验通过，正在安装，网络可能短暂重连...");
            await UdpProxyDriver.InstallAsync(driver.Path).ConfigureAwait(false);
            log("UDP 驱动准备完成；未启动转发，也未注册 ProxiFyre Windows 服务。");
        }
        finally { Gate.Release(); }
    }
}
