using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher;

public sealed class BuiltinUdpProxyService(Action<string>? log = null) : IUdpProxyService
{
    public async Task<IUdpProxySession> StartAsync(GameTaskConfig task, CancellationToken cancellationToken)
    {
        var targets = UdpProxyRules.Validate(task);
        UdpProxyPaths.RequireAdministrator();
        if (!File.Exists(UdpProxyPaths.EngineExecutable))
            throw new FileNotFoundException("内置 UDP 组件缺失，请重新解压完整 MAAA 发布包。", UdpProxyPaths.EngineExecutable);
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
        if (!UdpProxyDriver.HasDriver())
            await UdpProxySetup.PrepareAsync(log ?? (_ => { }), cancellationToken).ConfigureAwait(false);
        var upstream = await TaskUdpUpstream.StartAsync(task.UdpProxy, log ?? (_ => { }), cancellationToken)
            .ConfigureAwait(false);
        BuiltinUdpProxySession? session = null;
        try
        {
            await Socks5UdpProbe.CheckAsync(upstream?.Endpoint ?? task.UdpProxy, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            session = BuiltinUdpProxySession.Start(UdpProxyRules.BuildArguments(task, upstream?.Endpoint));
            await session.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
            return upstream is null ? session : new TaskUdpProxySession(session, upstream);
        }
        catch
        {
            try { if (session is not null) await session.DisposeAsync().ConfigureAwait(false); }
            finally { if (upstream is not null) await upstream.DisposeAsync().ConfigureAwait(false); }
            throw;
        }
    }
}
