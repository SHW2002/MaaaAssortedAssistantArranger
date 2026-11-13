namespace Game_Daily_Routine_Launcher.Tests;

internal sealed class TaskFakes : IUdpProxyService, ITaskProcessService, IUdpProxySession
{
    public List<string> Events { get; } = [];
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Launched { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ProcessExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource EngineExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Disposing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource AllowDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool DelayReady { get; set; }
    public bool DelayDispose { get; set; }
    public bool FailStart { get; set; }
    public bool FailLaunch { get; set; }
    public bool SimulateTimeout { get; set; }
    public Task Exited => EngineExit.Task;

    public async Task<IUdpProxySession> StartAsync(GameTaskConfig task, CancellationToken cancellationToken)
    {
        Events.Add("proxy-start");
        if (DelayReady) await Ready.Task.WaitAsync(cancellationToken);
        if (FailStart) throw new InvalidOperationException("Simulated engine startup failure");
        Events.Add("proxy-ready");
        return this;
    }

    public void Launch(GameTaskConfig task)
    {
        Events.Add("launch");
        if (FailLaunch) throw new InvalidOperationException("Simulated launcher failure");
        Launched.TrySetResult();
    }

    public async Task WaitForExitAsync(GameTaskConfig task, Action onStarted, CancellationToken cancellationToken)
    {
        Events.Add("monitor");
        onStarted();
        try
        {
            if (SimulateTimeout) throw new OperationCanceledException();
            await ProcessExit.Task.WaitAsync(cancellationToken);
        }
        finally { Events.Add("monitor-ended"); }
    }

    public async ValueTask DisposeAsync()
    {
        Events.Add("proxy-stop");
        Disposing.TrySetResult();
        if (DelayDispose) await AllowDispose.Task;
        Events.Add("proxy-stopped");
    }

    public TaskChainRunner Runner() => new(new AudioService(), udpProxy: this, processes: this);

    public static GameTaskConfig Config(bool enabled = true) => new()
    {
        Id = "udp-test", Name = "UDP test", GameProcessName = "StarRail", ToolPath = "unused.exe",
        UdpProxy = new() { Enabled = enabled, ProcessNames = "StarRail.exe" }
    };
}
