using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Game_Daily_Routine_Launcher;

internal sealed class TaskUdpUpstream : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ProxyJob _job;
    private readonly PrivateInstallDirectory _directory;
    public UdpProxyConfig Endpoint { get; }
    public Task Exited { get; }

    private TaskUdpUpstream(Process process, ProxyJob job, PrivateInstallDirectory directory, int port)
    {
        _process = process; _job = job; _directory = directory;
        Endpoint = new() { Enabled = true, Socks5Host = "127.0.0.1", Socks5Port = port };
        Exited = process.WaitForExitAsync();
    }

    public static async Task<TaskUdpUpstream?> StartAsync(UdpProxyConfig configured,
        Action<string> log, CancellationToken cancellationToken)
    {
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        var route = FlClashTaskSource.TryRead(configured, port);
        if (route is null) return null;
        var binary = Path.Combine(UdpProxyPaths.EngineDirectory, "upstream", "mihomo.exe");
        if (!File.Exists(binary)) throw new FileNotFoundException("内置任务上游组件缺失，请重新解压完整发布包。", binary);
        reservation.Stop();
        var directory = new PrivateInstallDirectory();
        ProxyJob? job = null;
        Process? process = null;
        TaskUdpUpstream? session = null;
        try
        {
            var config = Path.Combine(directory.Path, "config.json");
            await File.WriteAllTextAsync(config, route.Configuration, cancellationToken).ConfigureAwait(false);
            job = ProxyJob.Create();
            var info = new ProcessStartInfo(binary)
            {
                WorkingDirectory = directory.Path, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "-d", directory.Path, "-f", config }) info.ArgumentList.Add(argument);
            process = Process.Start(info) ?? throw new IOException("无法启动内置任务上游。");
            job.Assign(process);
            // Drain both pipes, but never put subscription credentials or raw core errors into the UI log.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            session = new(process, job, directory, port);
            using var readiness = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readiness.CancelAfter(TimeSpan.FromSeconds(15));
            while (true)
            {
                readiness.Token.ThrowIfCancellationRequested();
                if (session.Exited.IsCompleted) throw new IOException("内置任务上游启动失败，请检查当前节点及代理链配置。");
                try
                {
                    await Socks5UdpProbe.CheckAsync(session.Endpoint, readiness.Token).ConfigureAwait(false);
                    break;
                }
                catch (SocketException) { await Task.Delay(100, readiness.Token).ConfigureAwait(false); }
            }
            log($"任务独立出口已就绪：{route.NodeName}；此任务 UDP 不再经过 FlClash 的直连分流。");
            return session;
        }
        catch
        {
            if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
            else
            {
                job?.Dispose();
                if (process is not null)
                {
                    try
                    {
                        if (!process.HasExited) process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                    finally { process.Dispose(); }
                }
                directory.Dispose();
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _job.Dispose();
        try { await Exited.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        finally { _process.Dispose(); _directory.Dispose(); }
    }
}
