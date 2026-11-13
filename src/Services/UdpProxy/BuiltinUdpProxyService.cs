using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher;

public sealed class BuiltinUdpProxyService : IUdpProxyService
{
    public async Task<IUdpProxySession> StartAsync(GameTaskConfig task, CancellationToken cancellationToken)
    {
        var targets = UdpProxyRules.Validate(task);
        UdpProxyPaths.RequireAdministrator();
        if (!File.Exists(UdpProxyPaths.EngineExecutable))
            throw new FileNotFoundException("内置 UDP 组件缺失，请重新解压完整 MAAA 发布包。", UdpProxyPaths.EngineExecutable);
        if (!UdpProxyDriver.HasDriver())
            throw new InvalidOperationException("尚未安装 Windows Packet Filter 驱动，请在任务配置中点击‘准备 UDP 组件’。");
        var engines = Process.GetProcessesByName("ProxiFyre");
        try
        {
            if (engines.Length > 0)
                throw new InvalidOperationException("检测到 ProxiFyre 正在使用网络过滤驱动，请先退出，避免同时接管网络。");
        }
        finally { foreach (var engine in engines) engine.Dispose(); }
        foreach (var target in targets)
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target));
            try
            {
                if (processes.Length > 0)
                    throw new InvalidOperationException($"{target} 已在运行，请先退出，确保 UDP 代理先于游戏启动。");
            }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        await Socks5UdpProbe.CheckAsync(task.UdpProxy, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var session = BuiltinUdpProxySession.Start(UdpProxyRules.BuildArguments(task));
        try
        {
            await session.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
