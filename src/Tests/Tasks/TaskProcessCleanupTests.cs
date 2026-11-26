using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Game_Daily_Routine_Launcher.Tests;

public class TaskProcessCleanupTests
{
    [Fact]
    public void ExistingTasksReceiveDefaultsOnlyWhenTheirListIsMissing()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var config = JsonSerializer.Deserialize<AppConfig>("""
            { "tasks": [
                  { "id": "maa" },
                  { "id": "march7th" },
                  { "id": "zzz" },
                  { "id": "genshin", "gameProcessName": "GenshinImpact.EXE" },
                  { "id": "maaend" },
                  { "id": "ww" },
                  { "id": "maaend", "relatedProcessNames": "MyRelay.exe" },
                  { "id": "zzz", "relatedProcessNames": "" },
                  { "id": "custom", "gameProcessName": "MyGame" }
            ] }
            """, options)!;

        Assert.True(TaskProcessCleanupDefaults.Apply(config.Tasks));
        Assert.Equal(new[] { "MuMuNxMain.exe, MuMuNxDevice.exe", "StarRail.exe", "ZenlessZoneZero.exe",
            "BetterGI.exe, GenshinImpact.exe", "Endfield.exe", "Wuthering Waves.exe, Client-Win64-Shipping.exe",
            "MyRelay.exe", "", null }, config.Tasks.Select(task => task.RelatedProcessNames));
        Assert.False(TaskProcessCleanupDefaults.Apply(config.Tasks));
        Assert.Equal("", config.Tasks[0].GameProcessName);
    }

    [Fact]
    public async Task CleanupWaitsForTheLaunchedProcessWhenThereIsNoMonitor()
    {
        var fake = new TaskFakes();
        using var runner = fake.Runner();
        var run = runner.RunChainAsync([new() { RelatedProcessNames = "Example.exe" }, new()], false);
        Assert.Equal(new[] { "launch" }, fake.Events);

        fake.ProcessExit.SetResult();

        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { "launch", "terminate", "launch" }, fake.Events);
    }

    [Fact]
    public async Task TerminationStopsTheMainAndAllNamedProcesses()
    {
        var ping = Path.Combine(Environment.SystemDirectory, "ping.exe");
        var directory = Directory.CreateTempSubdirectory();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var firstName = $"MaaaGame-{suffix}";
        var secondName = $"MaaaRelay-{suffix}";
        var firstPath = Path.Combine(directory.FullName, firstName + ".exe");
        var secondPath = Path.Combine(directory.FullName, secondName + ".exe");
        var processes = new List<Process>();
        try
        {
            File.Copy(ping, firstPath);
            File.Copy(ping, secondPath);
            // Ping needs its localized resources to run after being copied out of System32.
            foreach (var culture in new[] { "en-US", System.Globalization.CultureInfo.InstalledUICulture.Name }.Distinct())
            {
                var resource = Path.Combine(Environment.SystemDirectory, culture, "ping.exe.mui");
                if (!File.Exists(resource)) continue;
                var destination = Directory.CreateDirectory(Path.Combine(directory.FullName, culture)).FullName;
                File.Copy(resource, Path.Combine(destination, firstName + ".exe.mui"));
                File.Copy(resource, Path.Combine(destination, secondName + ".exe.mui"));
            }

            foreach (var path in new[] { ping, firstPath, firstPath, secondPath })
                processes.Add(Process.Start(new ProcessStartInfo(path, "-t 127.0.0.1")
                    { UseShellExecute = false, CreateNoWindow = true })!);

            var names = TaskProcessNames.ForCleanup($" {firstName}.exe；{secondName}, {firstName.ToUpperInvariant()}\n");
            Assert.Equal(new[] { firstName, secondName }, names);
            Assert.All(processes, process => Assert.False(process.HasExited));

            using var session = new TaskProcessSession(Process.GetProcessById(processes[0].Id), string.Empty, names);
            await session.TerminateAsync();

            Assert.All(processes, process => Assert.True(process.HasExited));
        }
        finally
        {
            foreach (var process in processes)
            {
                using (process)
                {
                    if (!process.HasExited) process.Kill();
                    await process.WaitForExitAsync();
                }
            }
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}
