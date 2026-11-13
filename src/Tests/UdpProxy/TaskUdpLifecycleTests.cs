namespace Game_Daily_Routine_Launcher.Tests;

public class TaskUdpLifecycleTests
{
    [Fact]
    public async Task AutomaticTargetsAreReportedInLogsInsteadOfTheEmptyOverride()
    {
        var fake = new TaskFakes();
        fake.ProcessExit.SetResult();
        var task = TaskFakes.Config();
        task.Id = "march7th";
        task.UdpProxy.ProcessNames = string.Empty;
        var messages = new List<string>();
        using var runner = fake.Runner();
        runner.LogMessage += messages.Add;
        Assert.True(await runner.RunChainAsync([task], false));
        var target = UdpProxyRules.BuildArguments(task).Last();
        Assert.Contains($"UDP 代理已就绪，仅匹配：{target}。", messages);
    }

    [Fact]
    public async Task DisabledTasksPreserveNormalLaunchWithoutStartingProxy()
    {
        var fake = new TaskFakes();
        fake.ProcessExit.SetResult();
        using var runner = fake.Runner();
        Assert.True(await runner.RunChainAsync([TaskFakes.Config(false)], false));
        Assert.Equal(new[] { "monitor", "monitor-ended", "launch" }, fake.Events);
    }

    [Fact]
    public async Task WaitsForProxyReadinessAndStopsAfterMonitoredProcessExits()
    {
        var fake = new TaskFakes { DelayReady = true };
        using var runner = fake.Runner();
        var run = runner.RunChainAsync([TaskFakes.Config()], false);
        Assert.Equal(new[] { "proxy-start" }, fake.Events);
        fake.Ready.SetResult();
        await fake.Launched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain("proxy-stop", fake.Events);
        fake.ProcessExit.SetResult();
        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { "proxy-start", "proxy-ready", "monitor", "launch", "monitor-ended",
            "proxy-stop", "proxy-stopped" }, fake.Events);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task HandlesProxyStartupFailureAndToolLaunchFailure(bool start, bool launch)
    {
        var fake = new TaskFakes { FailStart = start, FailLaunch = launch };
        using var runner = fake.Runner();
        var states = new List<string>();
        runner.TaskStateChanged += (_, state) => states.Add(state);
        Assert.False(await runner.RunChainAsync([TaskFakes.Config()], false));
        Assert.Contains("Error", states);
        Assert.DoesNotContain("Completed", states);
        if (start) Assert.DoesNotContain("launch", fake.Events);
        else Assert.Contains("proxy-stopped", fake.Events);
    }

    [Fact]
    public async Task TimeoutClosesProxyAndReportsFailure()
    {
        var fake = new TaskFakes { SimulateTimeout = true };
        using var runner = fake.Runner();
        var states = new List<string>();
        runner.TaskStateChanged += (_, state) => states.Add(state);
        Assert.False(await runner.RunChainAsync([TaskFakes.Config()], false));
        Assert.Contains("TimedOut", states);
        Assert.Contains("proxy-stopped", fake.Events);
    }

    [Fact]
    public async Task StopAsyncWaitsForProxyCleanupBeforeReturning()
    {
        var fake = new TaskFakes { DelayDispose = true };
        using var runner = fake.Runner();
        var run = runner.RunChainAsync([TaskFakes.Config()], false);
        await fake.Launched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = runner.StopAsync();
        await fake.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(stop.IsCompleted);
        fake.AllowDispose.SetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(await run);
        Assert.Contains("proxy-stopped", fake.Events);
    }

    [Fact]
    public async Task CancellationWhilePreparingDoesNotLaunchTool()
    {
        var fake = new TaskFakes { DelayReady = true };
        using var runner = fake.Runner();
        var run = runner.RunChainAsync([TaskFakes.Config()], false);
        await runner.StopAsync();
        Assert.False(await run);
        Assert.DoesNotContain("launch", fake.Events);
    }

    [Fact]
    public async Task EngineFailureWhileMonitoringStopsProxyAndCancelsMonitor()
    {
        var fake = new TaskFakes();
        using var runner = fake.Runner();
        var run = runner.RunChainAsync([TaskFakes.Config()], false);
        await fake.Launched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fake.EngineExit.SetResult();
        Assert.False(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Contains("monitor-ended", fake.Events);
        Assert.Contains("proxy-stopped", fake.Events);
    }

    [Fact]
    public async Task UpstreamConflictIsRejectedBeforeGlobalPreparation()
    {
        var fake = new TaskFakes();
        var prepared = false;
        using var runner = new TaskChainRunner(new AudioService(), udpProxy: fake, processes: fake)
        {
            ValidateTasks = tasks => UdpProxyRules.ValidateUpstreamCompatibility(tasks, true),
            BeforeTaskAsync = _ => { prepared = true; return Task.CompletedTask; }
        };
        Assert.False(await runner.RunChainAsync([TaskFakes.Config()], false));
        Assert.False(prepared);
        Assert.Empty(fake.Events);
    }

    [Fact]
    public async Task EachEnabledTaskOwnsItsProxyUntilItsOwnCompletion()
    {
        var fake = new TaskFakes();
        fake.ProcessExit.SetResult();
        using var runner = fake.Runner();
        var disabled = TaskFakes.Config();
        disabled.Enabled = false;
        Assert.True(await runner.RunChainAsync([TaskFakes.Config(), disabled, TaskFakes.Config()], false));
        Assert.Equal(2, fake.Events.Count(entry => entry == "proxy-start"));
        var firstStop = fake.Events.IndexOf("proxy-stopped");
        Assert.True(firstStop < fake.Events.LastIndexOf("proxy-start"));
    }
}
