using System.Text.Json;

namespace Game_Daily_Routine_Launcher.Tests;

public class UdpProxyConfigurationTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"udpProxy\":null}")]
    public void OldAndNullConfigurationKeepProxyDisabled(string json)
    {
        var task = JsonSerializer.Deserialize<GameTaskConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.False(task.UdpProxy.Enabled);
        Assert.Empty(UdpProxyRules.Validate(task));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.exe")]
    [InlineData("C:\\Games\\StarRail.exe")]
    [InlineData("ProxiFyre.exe")]
    [InlineData("StarRail")]
    [InlineData(".exe")]
    [InlineData(" ; , ")]
    public void RejectsCatchAllDirectoryWildcardAndEngineTargets(string targets)
    {
        var task = TaskFakes.Config();
        task.UdpProxy.ProcessNames = targets;
        Assert.Throws<InvalidOperationException>(() => UdpProxyRules.BuildArguments(task));
    }

    [Fact]
    public void ArgumentsContainOnlyAnEndpointAndExplicitDeduplicatedPrograms()
    {
        var task = TaskFakes.Config();
        task.UdpProxy.ProcessNames = "StarRail.exe; starrail.EXE\r\nAnotherGame.exe";
        Assert.Equal(new[] { "--endpoint", "127.0.0.1:7890", "--app", "StarRail.exe", "--app", "AnotherGame.exe" },
            UdpProxyRules.BuildArguments(task));
    }

    [Fact]
    public void EnabledProxyRequiresLifecycleMonitor()
    {
        var task = TaskFakes.Config();
        task.GameProcessName = "";
        Assert.Throws<InvalidOperationException>(() => UdpProxyRules.Validate(task));
    }

    [Theory]
    [InlineData("127.0.0.1", 7890, true)]
    [InlineData("localhost", 1080, true)]
    [InlineData("proxy.example.com", 1080, false)]
    public void DetectsLocalUpstreamConflictWithoutChangingSettings(string host, int port, bool conflict)
    {
        var task = TaskFakes.Config();
        task.UdpProxy.Socks5Host = host;
        task.UdpProxy.Socks5Port = port;
        if (conflict) Assert.Throws<InvalidOperationException>(() => UdpProxyRules.ValidateUpstreamCompatibility([task], true));
        else UdpProxyRules.ValidateUpstreamCompatibility([task], true);
        Assert.Equal(host, task.UdpProxy.Socks5Host);
        UdpProxyRules.ValidateUpstreamCompatibility([task], false);
    }

    [Theory]
    [InlineData("http://127.0.0.1", 7890)]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", 65536)]
    [InlineData("::1", 1080)]
    public void RejectsUnsupportedEndpoint(string host, int port)
    {
        var task = TaskFakes.Config();
        task.UdpProxy.Socks5Host = host;
        task.UdpProxy.Socks5Port = port;
        Assert.Throws<InvalidOperationException>(() => UdpProxyRules.Validate(task));
    }

    [Fact]
    public void ViewModelChangesPersistWithAutomaticSaveNotifications()
    {
        var vm = new GameTaskViewModel(TaskFakes.Config(false));
        var changes = 0;
        vm.ConfigChanged = () => changes++;
        vm.UdpProxyEnabled = true;
        vm.UdpProxyProcessNames = "StarRail.exe;Other.exe";
        vm.UdpProxyHost = "localhost";
        vm.UdpProxyPort = 1080;
        var reloaded = JsonSerializer.Deserialize<GameTaskConfig>(JsonSerializer.Serialize(vm.ToConfig()))!;
        Assert.True(changes >= 4);
        Assert.True(reloaded.UdpProxy.Enabled);
        Assert.Equal("StarRail.exe;Other.exe", reloaded.UdpProxy.ProcessNames);
        Assert.Equal("localhost", reloaded.UdpProxy.Socks5Host);
        Assert.Equal(1080, reloaded.UdpProxy.Socks5Port);
    }
}
