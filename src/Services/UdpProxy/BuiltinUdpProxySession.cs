using System.Diagnostics;
using System.IO;
using System.Text;

namespace Game_Daily_Routine_Launcher;

internal sealed class BuiltinUdpProxySession : IUdpProxySession
{
    private readonly Process _process;
    private readonly ProxyJob _job;
    private readonly object _gate = new();
    private readonly StringBuilder _diagnostics = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _disposeTask;
    public Task Exited { get; }

    private BuiltinUdpProxySession(Process process, ProxyJob job)
    {
        _process = process;
        _job = job;
        Exited = process.WaitForExitAsync();
        process.OutputDataReceived += (_, data) =>
        {
            if (data.Data == "MAAA_UDP_READY") _ready.TrySetResult();
        };
        process.ErrorDataReceived += (_, data) =>
        {
            if (data.Data is null) return;
            lock (_diagnostics)
            {
                _diagnostics.AppendLine(data.Data);
                if (_diagnostics.Length > 2000) _diagnostics.Remove(0, _diagnostics.Length - 2000);
            }
        };
    }

    public static BuiltinUdpProxySession Start(IReadOnlyList<string> arguments)
    {
        var job = ProxyJob.Create();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(UdpProxyPaths.EngineExecutable)
            {
                WorkingDirectory = UdpProxyPaths.EngineDirectory,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            if (!process.Start()) throw new IOException("内置 UDP 组件启动失败。");
            job.Assign(process);
            var session = new BuiltinUdpProxySession(process, job);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return session;
        }
        catch
        {
            try { if (process.Id > 0 && !process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            finally { job.Dispose(); process.Dispose(); }
            throw;
        }
    }

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var completed = await Task.WhenAny(_ready.Task, Exited).WaitAsync(timeout.Token).ConfigureAwait(false);
            if (completed == Exited || Exited.IsCompleted)
            {
                lock (_diagnostics) throw new IOException($"内置 UDP 组件启动失败：{_diagnostics}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("内置 UDP 组件未能在 20 秒内就绪，请检查驱动及管理员权限。");
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate) return new(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            if (!Exited.IsCompleted)
            {
                try
                {
                    await _process.StandardInput.WriteLineAsync("stop").WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    await _process.StandardInput.FlushAsync().WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
                catch (IOException) { }
                catch (InvalidOperationException) { }
                catch (TimeoutException) { }
                try { await Exited.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch (TimeoutException)
                {
                    _job.Dispose();
                    await Exited.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
            }
        }
        finally { _job.Dispose(); _process.Dispose(); }
    }
}
