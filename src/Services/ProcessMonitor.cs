using System.Diagnostics;

namespace Game_Daily_Routine_Launcher;

public class ProcessMonitor : IDisposable
{
    private readonly string _processName;
    private readonly int _pollIntervalMs;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();
    private Task? _monitorTask;
    private bool _disposed;

    public event Action? ProcessStarted;
    public event Action? ProcessExited;

    public bool IsActive { get; private set; }

    public ProcessMonitor(string processName, int pollIntervalMs = 60000)
    {
        _processName = processName;
        _pollIntervalMs = pollIntervalMs;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _monitorTask = Task.Run(() => MonitorLoop(_cts.Token));
        }
    }

    public void Stop()
    {
        Task? monitorTask;

        lock (_gate)
        {
            if (_disposed)
                return;

            _cts.Cancel();
            monitorTask = _monitorTask;
        }

        WaitForMonitorTask(monitorTask);
    }

    public async Task StopAsync()
    {
        Task? monitorTask;
        lock (_gate)
        {
            if (_disposed) return;
            _cts.Cancel();
            monitorTask = _monitorTask;
        }
        if (monitorTask is not null) await monitorTask.ConfigureAwait(false);
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        var wasRunning = false;

        while (!ct.IsCancellationRequested)
        {
            var isRunning = IsProcessRunning();

            if (isRunning && !wasRunning)
            {
                IsActive = true;
                ProcessStarted?.Invoke();
            }
            else if (!isRunning && wasRunning)
            {
                IsActive = false;
                ProcessExited?.Invoke();
                return;
            }

            wasRunning = isRunning;

            try
            {
                await Task.Delay(_pollIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private bool IsProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(_processName);
            try { return processes.Length > 0; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Task? monitorTask;

        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts.Cancel();
            monitorTask = _monitorTask;
        }

        WaitForMonitorTask(monitorTask);
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void WaitForMonitorTask(Task? monitorTask)
    {
        if (monitorTask == null || monitorTask.Id == Task.CurrentId)
            return;

        monitorTask.Wait(TimeSpan.FromSeconds(5));
    }
}
