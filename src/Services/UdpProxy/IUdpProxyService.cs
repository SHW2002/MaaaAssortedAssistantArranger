namespace Game_Daily_Routine_Launcher;

public interface IUdpProxyService
{
    Task<IUdpProxySession> StartAsync(GameTaskConfig task, CancellationToken cancellationToken);
}

public interface IUdpProxySession : IAsyncDisposable
{
    Task Exited { get; }
}
