using System.Text.Json.Nodes;
using Xunit;

namespace Game_Daily_Routine_Launcher.Tests;

public sealed class FlClashTaskRouteTests
{
    private static JsonObject Preferences(string choice = "Fast") => new()
    {
        ["currentProfileId"] = 7,
        ["profiles"] = new JsonArray(new JsonObject
        {
            ["id"] = 7, ["selectedMap"] = new JsonObject { ["Default"] = "Region", ["Region"] = choice }
        })
    };

    private static JsonObject Source() => FlClashTaskRouteBuilder.ParseYaml("""
        mixed-port: 7890
        external-controller: 0.0.0.0:9090
        proxies:
          - {name: Fast, type: ss, server: example.invalid, port: 443, password: test, udp: true, dialer-proxy: Hop}
          - {name: Hop, type: socks5, server: example.invalid, port: 1080}
          - {name: Unused, type: socks5, server: example.invalid, port: 1081}
        proxy-groups:
          - {name: Default, type: select, proxies: [DIRECT, Region]}
          - {name: Region, type: select, proxies: [Fast, DIRECT]}
        rules: ["PROCESS-NAME,StarRail.exe,DIRECT", "GEOIP,CN,DIRECT", "MATCH,Default"]
        """);

    [Fact]
    public void IsolatedRouteFollowsSelectedNodeWithoutImportingDirectRulesOrChangingSource()
    {
        var source = Source();
        var preferences = Preferences();
        var original = source.ToJsonString();
        var originalPreferences = preferences.ToJsonString();
        var route = FlClashTaskRouteBuilder.Build(source, preferences, 18000);
        var isolated = JsonNode.Parse(route.Configuration)!;
        Assert.Equal("Fast", route.NodeName);
        Assert.Equal(original, source.ToJsonString());
        Assert.Equal(originalPreferences, preferences.ToJsonString());
        Assert.Equal(18000, isolated["mixed-port"]!.GetValue<int>());
        Assert.Equal("127.0.0.1", isolated["bind-address"]!.GetValue<string>());
        Assert.False(isolated["allow-lan"]!.GetValue<bool>());
        Assert.False(isolated["tun"]!["enable"]!.GetValue<bool>());
        Assert.Null(isolated["external-controller"]);
        Assert.Equal("MATCH,MAAA-selected", Assert.Single(isolated["rules"]!.AsArray())!.GetValue<string>());
        var nodes = isolated["proxies"]!.AsArray();
        Assert.Equal(2, nodes.Count);
        Assert.DoesNotContain(nodes, item => item!["name"]!.ToString() == "Unused");
        var selected = nodes.Single(item => item!["name"]!.ToString() == "MAAA-selected")!;
        Assert.Equal(443, selected["port"]!.GetValue<int>());
        Assert.True(selected["udp"]!.GetValue<bool>());
        Assert.Equal("Hop", selected["dialer-proxy"]!.ToString());
    }

    [Theory]
    [InlineData("DIRECT")]
    [InlineData("Uncached provider node")]
    public void RejectsDirectOrUnavailableSelection(string choice) =>
        Assert.Throws<InvalidOperationException>(() => FlClashTaskRouteBuilder.Build(Source(), Preferences(choice), 18000));

    [Fact]
    public void RejectsUdpDisabledNode()
    {
        var source = Source();
        source["proxies"]![0]!["udp"] = false;
        Assert.Throws<InvalidOperationException>(() => FlClashTaskRouteBuilder.Build(source, Preferences(), 18000));
    }

    [Fact]
    public void RejectsCyclicGroup()
    {
        var source = Source();
        source["proxy-groups"]![1]!["proxies"] = new JsonArray("Default");
        Assert.Throws<InvalidOperationException>(() => FlClashTaskRouteBuilder.Build(source, Preferences(), 18000));
    }

    [Fact]
    public void RejectsCyclicDialerDependency()
    {
        var source = Source();
        source["proxies"]![1]!["dialer-proxy"] = "Fast";
        Assert.Throws<InvalidOperationException>(() => FlClashTaskRouteBuilder.Build(source, Preferences(), 18000));
    }
}
