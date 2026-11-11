using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher;

/// <summary>Remembers only applications actually closed by this session.</summary>
public sealed class FlClashService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _closedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _log;
    private bool _cleaningUp;

    public FlClashService(Action<string> log) => _log = log;

    public async Task CloseAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_cleaningUp)
                throw new OperationCanceledException("程序正在退出。", ct);

            foreach (var process in Process.GetProcessesByName("FlClash"))
            {
                using (process)
                {
                    if (process.HasExited)
                        continue;

                    var path = process.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        throw new InvalidOperationException("无法记录 FlClash 路径，已取消关闭以保证能够恢复。");

                    // Remember the executable before sending any exit request.
                    _closedPaths.Add(path);
                    if (process.CloseMainWindow())
                    {
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        try { await process.WaitForExitAsync(timeout.Token); }
                        catch (OperationCanceledException) { }
                    }

                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(ct);
                    _log("已关闭 FlClash，任务结束后将恢复。");
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async Task RestoreAsync(bool cleaningUp = false)
    {
        await _gate.WaitAsync();
        try
        {
            _cleaningUp |= cleaningUp;
            foreach (var path in _closedPaths.ToArray())
            {
                try
                {
                    var running = Process.GetProcessesByName("FlClash");
                    bool alreadyRunning;
                    try { alreadyRunning = running.Any(p => !p.HasExited); }
                    finally { foreach (var process in running) process.Dispose(); }

                    if (!alreadyRunning)
                    {
                        using var started = Process.Start(new ProcessStartInfo(path)
                        {
                            WorkingDirectory = Path.GetDirectoryName(path),
                            UseShellExecute = true
                        });
                        if (started is null)
                            throw new InvalidOperationException("未能创建 FlClash 进程。");
                        _log("已重新启动 FlClash。");
                    }
                    _closedPaths.Remove(path);
                }
                catch (Exception ex)
                {
                    _log($"恢复 FlClash 失败：{ex.Message}");
                }
            }
        }
        finally { _gate.Release(); }
    }
}
