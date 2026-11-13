using System.Text.Json;

namespace Game_Daily_Routine_Launcher.Tests;

public class UdpProxyProcessResolverTests
{
    [Theory]
    [InlineData("march7th", "March7thAssistant", "StarRail.exe")]
    [InlineData("zzz", "OneDragon", "ZenlessZoneZero.exe")]
    [InlineData("genshin", "YuanShen", "YuanShen.exe")]
    [InlineData("genshin", "GenshinImpact.EXE", "GenshinImpact.exe")]
    [InlineData("maaend", "MaaEnd", "Endfield.exe")]
    [InlineData("ww", "Wuthering Waves", "Client-Win64-Shipping.exe")]
    public void BuiltinTasksTargetTheGameInsteadOfTheirAutomationTool(string id, string monitor, string target)
    {
        var task = AutomaticTask(id, monitor);
        Assert.Equal(new[] { target }, UdpProxyRules.Validate(task));
        Assert.Equal(new[] { "--endpoint", "127.0.0.1:7890", "--app", target },
            UdpProxyRules.BuildArguments(task));
        Assert.Empty(task.UdpProxy.ProcessNames);
    }

    [Theory]
    [InlineData("星穹铁道 (March7th)", "assistant", "StarRail.exe")]
    [InlineData("Renamed task", "StarRail", "StarRail.exe")]
    [InlineData("绝区零 (OneDragon)", "assistant", "ZenlessZoneZero.exe")]
    public void ImportedTasksCanResolveFromTheirNameOrKnownGameMonitor(string name, string monitor, string target)
    {
        var task = AutomaticTask("imported-task", monitor);
        task.Name = name;
        Assert.Equal(new[] { target }, UdpProxyRules.Validate(task));
    }

    [Theory]
    [InlineData("Other.Game", "Other.Game.exe")]
    [InlineData(" Other.Game.EXE ", "Other.Game.exe")]
    [InlineData("MuMuPlayer", "MuMuPlayer.exe")]
    public void UnknownGamesAndEmulatorsFollowTheExactMonitor(string monitor, string target)
    {
        var task = AutomaticTask("maa", monitor);
        Assert.Equal(new[] { target }, UdpProxyRules.Validate(task));
    }

    [Fact]
    public void ManualTargetsOverrideThePresetAndClearingThemRestoresAutomaticMode()
    {
        var task = AutomaticTask("march7th", "StarRail");
        task.UdpProxy.ProcessNames = "Custom.exe;custom.EXE\r\nAnother.exe";
        Assert.Equal(new[] { "Custom.exe", "Another.exe" }, UdpProxyRules.Validate(task));
        task.UdpProxy.ProcessNames = " \r\n ";
        Assert.Equal(new[] { "StarRail.exe" }, UdpProxyRules.Validate(task));
    }

    [Fact]
    public void EnabledLegacyConfigurationWithEmptyTargetsUsesAutomaticModeWithoutMigration()
    {
        var task = JsonSerializer.Deserialize<GameTaskConfig>(
            """{"Id":"march7th","GameProcessName":"StarRail","UdpProxy":{"Enabled":true}}""")!;
        Assert.Equal(new[] { "StarRail.exe" }, UdpProxyRules.Validate(task));
        var reloaded = JsonSerializer.Deserialize<GameTaskConfig>(JsonSerializer.Serialize(task))!;
        Assert.Empty(reloaded.UdpProxy.ProcessNames);
        Assert.Equal(new[] { "StarRail.exe" }, UdpProxyRules.Validate(reloaded));
    }

    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("C:\\Games\\Game.exe")]
    [InlineData("MaaaUdpProxy")]
    [InlineData("ProxiFyre")]
    public void AutomaticModeStillRejectsMissingWildcardPathAndEngineTargets(string monitor)
    {
        var task = AutomaticTask("unknown", monitor);
        Assert.Throws<InvalidOperationException>(() => UdpProxyRules.BuildArguments(task));
    }

    private static GameTaskConfig AutomaticTask(string id, string monitor)
    {
        var task = TaskFakes.Config();
        task.Id = id;
        task.GameProcessName = monitor;
        task.UdpProxy.ProcessNames = string.Empty;
        return task;
    }
}
