namespace Game_Daily_Routine_Launcher;

public enum TaskState
{
    Idle,
    Launching,
    Running,
    MonitoringWaitStart,
    MonitoringWaitStop,
    Completed,
    TimedOut,
    Error,
    Terminated
}
