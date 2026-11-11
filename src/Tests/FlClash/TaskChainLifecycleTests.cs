namespace Game_Daily_Routine_Launcher.Tests;

public class TaskChainLifecycleTests
{
    [Fact]
    public async Task ChecksAtChainStartAndBeforeEachEnabledTaskAndRestoresOnce()
    {
        var checks = 0;
        var restores = 0;
        using var runner = new TaskChainRunner(new AudioService())
        {
            BeforeTaskAsync = _ => { checks++; return Task.CompletedTask; },
            AfterChainAsync = () => { restores++; return Task.CompletedTask; }
        };
        // Missing executables exercise launch failure without starting real programs.
        var missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".exe");
        await runner.RunChainAsync([
            new GameTaskConfig { Enabled = true, ToolPath = missing },
            new GameTaskConfig { Enabled = false, ToolPath = missing },
            new GameTaskConfig { Enabled = true, ToolPath = missing }
        ], false);

        Assert.Equal(3, checks);
        Assert.Equal(1, restores);
        Assert.False(runner.IsRunning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoresWhenPreparationFailsOrIsCancelled(bool cancel)
    {
        var restored = false;
        using var runner = new TaskChainRunner(new AudioService())
        {
            BeforeTaskAsync = _ => throw (cancel
                ? new OperationCanceledException()
                : new InvalidOperationException("Close failed")),
            AfterChainAsync = () => { restored = true; return Task.CompletedTask; }
        };

        Assert.False(await runner.RunChainAsync([], false));
        Assert.True(restored);
        Assert.False(runner.IsRunning);
    }
}
