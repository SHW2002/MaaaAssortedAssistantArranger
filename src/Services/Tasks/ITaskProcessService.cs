namespace Game_Daily_Routine_Launcher;

public interface ITaskProcessService
{
    void Launch(GameTaskConfig task);
    Task WaitForExitAsync(GameTaskConfig task, Action onStarted, CancellationToken cancellationToken);
}
