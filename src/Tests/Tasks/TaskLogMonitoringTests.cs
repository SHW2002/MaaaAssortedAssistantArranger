using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher.Tests;

public class TaskLogMonitoringTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedLogsResetTheCurrentDeadlineAndTerminationDrainsTheQueue(bool hasNextTask)
    {
        var clock = new ManualClock();
        var fake = new TaskFakes();
        using var runner = new TaskChainRunner(new AudioService(), udpProxy: fake, processes: fake) { Clock = clock };
        var states = new List<(string, string)>();
        runner.TaskStateChanged += (id, state) => states.Add((id, state));
        var task = new GameTaskConfig { Id = "task", LogMonitoringEnabled = true };
        List<GameTaskConfig> tasks = [task];
        if (hasNextTask) tasks.Add(new GameTaskConfig { Id = "next" });

        var run = runner.RunChainAsync(tasks, false);
        clock.Advance(TimeSpan.FromMinutes(9));
        runner.ReportTaskLog();
        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.False(run.IsCompleted);
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(runner.ReachedEnd);
        Assert.Contains(("task", "Terminated"), states);
        Assert.DoesNotContain(("task", "Completed"), states);
        Assert.Equal(hasNextTask ? ["launch", "terminate", "launch"] : new[] { "launch", "terminate" }, fake.Events);
    }

    [Fact]
    public async Task ExitBeforeDeadlineCompletesWithCleanup()
    {
        var fake = new TaskFakes();
        using var runner = fake.Runner();
        var task = new GameTaskConfig { Id = "task", LogMonitoringEnabled = true };
        var run = runner.RunChainAsync([task], false);
        Assert.False(run.IsCompleted);
        fake.ProcessExit.SetResult();

        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { "launch", "terminate" }, fake.Events);
    }

    [Fact]
    public async Task TerminationStopsTheProcessLaunchedForTheTask()
    {
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -Command Start-Sleep 30")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        using var session = new TaskProcessSession(process, string.Empty, []);
        try
        {
            await session.TerminateAsync();
            Assert.True(process.HasExited);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        private ManualTimer? _timer;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new ManualTimer(this, callback, state);
            _timer.Change(dueTime, period);
            return _timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            _timestamp += elapsed.Ticks;
            _timer?.Tick();
        }

        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long? _deadline;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _deadline = dueTime == Timeout.InfiniteTimeSpan ? null : clock.GetTimestamp() + dueTime.Ticks;
                return true;
            }

            public void Tick()
            {
                if (clock.GetTimestamp() < _deadline || _deadline is null) return;
                _deadline = null;
                callback(state);
            }

            public void Dispose() => _deadline = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
