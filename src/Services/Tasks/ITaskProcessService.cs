namespace Game_Daily_Routine_Launcher;

public interface ITaskProcessService
{
    ITaskProcessSession Launch(GameTaskConfig task);
    Task WaitForExitAsync(GameTaskConfig task, Action onStarted, CancellationToken cancellationToken);
}

public interface ITaskProcessSession : IDisposable
{
    Task WaitForExitAsync(CancellationToken cancellationToken);
    Task TerminateAsync();
}
