using System.Diagnostics;

namespace Game_Daily_Routine_Launcher.Tests;

public class UdpElevationTests
{
    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    public void OnlyEnabledUdpTasksInANonAdminProcessNeedElevation(
        bool admin, bool udp, bool enabled, bool expected)
    {
        var task = new GameTaskConfig { Enabled = enabled, UdpProxy = new() { Enabled = udp } };
        Assert.Equal(expected, UdpElevationService.NeedsElevation([task], admin));
        Assert.False(UdpElevationService.NeedsElevation([], admin));
    }

    [Theory]
    [InlineData(UdpElevationRequest.StartAll, -1, true)]
    [InlineData(UdpElevationRequest.StartSingle, 3, false)]
    [InlineData(UdpElevationRequest.Prepare, -1, false)]
    public void RelaunchPreservesOperationAndAutoRunContext(string operation, int taskIndex, bool summary)
    {
        var request = new UdpElevationRequest(operation, int.MaxValue, taskIndex, summary);
        var info = UdpElevationService.CreateStartInfo(
            @"C:\Program Files\MAAA\MAAA.exe", "unused.dll", request, true);
        Assert.Equal("runas", info.Verb);
        Assert.True(info.UseShellExecute);
        Assert.Equal(AppContext.BaseDirectory, info.WorkingDirectory);
        Assert.Equal(@"C:\Program Files\MAAA\MAAA.exe", info.FileName);
        Assert.Equal("--autorun", info.ArgumentList[0]);
        Assert.Equal(request, UdpElevationRequest.Parse(info.ArgumentList.ToArray()));
    }

    [Fact]
    public void DotnetHostedLaunchPassesAssemblyAsASeparateArgument()
    {
        var request = new UdpElevationRequest(UdpElevationRequest.StartSingle, int.MaxValue, 0);
        var info = UdpElevationService.CreateStartInfo(
            @"C:\Program Files\dotnet\dotnet.exe", @"C:\含 空格\MAAA.dll", request, false);
        Assert.Equal(@"C:\含 空格\MAAA.dll", info.ArgumentList[0]);
        Assert.DoesNotContain("--autorun", info.ArgumentList);
        Assert.Equal(request, UdpElevationRequest.Parse(info.ArgumentList.ToArray()));
    }

    [Theory]
    [InlineData("--udp-resume")]
    [InlineData("--udp-resume all 1")]
    [InlineData("--udp-resume unknown 1 -1 False")]
    [InlineData("--udp-resume all 0 -1 False")]
    [InlineData("--udp-resume all 1 0 False")]
    [InlineData("--udp-resume single 1 -1 False")]
    [InlineData("--udp-resume all 1 -1 invalid")]
    [InlineData("--udp-resume all 1 -1 False extra")]
    public void MalformedResumeArgumentsCannotStartAnUnintendedOperation(string arguments) =>
        Assert.Throws<InvalidOperationException>(() => UdpElevationRequest.Parse(arguments.Split(' ')));

    [Fact]
    public void NormalLaunchDoesNotResumeAndSelfWaitIsRejected()
    {
        Assert.Null(UdpElevationRequest.Parse(["--autorun"]));
        var self = new UdpElevationRequest(UdpElevationRequest.StartAll, Environment.ProcessId);
        Assert.Throws<InvalidOperationException>(() => UdpElevationRequest.Parse(self.ToArguments().ToArray()));
    }

    [Fact]
    public async Task AlreadyExitedParentDoesNotBlockResume() =>
        await UdpElevationService.WaitForParentExitAsync(int.MaxValue);

    [Fact]
    public async Task ResumeWaitsForTheOriginalProcessToExit()
    {
        using var parent = Process.Start(new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Milliseconds 500" }
        });
        Assert.NotNull(parent);
        await UdpElevationService.WaitForParentExitAsync(parent.Id);
        Assert.True(parent.HasExited);
    }
}
