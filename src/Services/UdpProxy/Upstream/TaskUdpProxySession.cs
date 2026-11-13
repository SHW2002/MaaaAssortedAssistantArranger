namespace Game_Daily_Routine_Launcher;

internal sealed class TaskUdpProxySession(IUdpProxySession engine, TaskUdpUpstream upstream) : IUdpProxySession
{
    public Task Exited { get; } = Task.WhenAny(engine.Exited, upstream.Exited);

    public async ValueTask DisposeAsync()
    {
        try { await engine.DisposeAsync().ConfigureAwait(false); }
        finally { await upstream.DisposeAsync().ConfigureAwait(false); }
    }
}
