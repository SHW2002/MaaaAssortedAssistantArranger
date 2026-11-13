using System.Diagnostics;
using System.IO;

namespace Game_Daily_Routine_Launcher.Tests;

public class NativeComponentTests
{
    [Theory]
    [InlineData("StarRail.exe", 0)]
    [InlineData("*.exe", 1)]
    [InlineData("C:\\Games\\StarRail.exe", 1)]
    [InlineData("MaaaUdpProxy.exe", 1)]
    public async Task BundledComponentValidatesWithoutLoadingDriver(string name, int expectedExit)
    {
        var info = new ProcessStartInfo(UdpProxyPaths.EngineExecutable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "--validate", "--endpoint", "127.0.0.1:7890", "--app", name })
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedExit, process.ExitCode);
        if (expectedExit == 0) Assert.Contains("MAAA_UDP_VALIDATED", await process.StandardOutput.ReadToEndAsync());
    }

    [Fact]
    public async Task EarlyExitIsNotMistakenForReadinessAndDisposalIsIdempotent()
    {
        var arguments = UdpProxyRules.BuildArguments(TaskFakes.Config()).Append("--validate").ToArray();
        var session = BuiltinUdpProxySession.Start(arguments);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => session.WaitUntilReadyAsync(CancellationToken.None));
        }
        finally
        {
            await session.DisposeAsync();
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClosingJobKillsOnlyItsOwnedChild()
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,
            @"WindowsPowerShell\v1.0\powershell.exe"), "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30")
        {
            UseShellExecute = false, CreateNoWindow = true
        })!;
        var job = ProxyJob.Create();
        try
        {
            job.Assign(process);
            Assert.False(process.HasExited);
            job.Dispose();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(process.HasExited);
        }
        finally
        {
            job.Dispose();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
