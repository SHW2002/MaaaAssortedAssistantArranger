using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game_Daily_Routine_Launcher.Tests;

public class AppearancePolicyTests
{
    [Theory]
    [InlineData(ThemeMode.System, false, false)]
    [InlineData(ThemeMode.System, true, true)]
    [InlineData(ThemeMode.Light, true, false)]
    [InlineData(ThemeMode.Dark, false, true)]
    [InlineData((ThemeMode)999, true, true)]
    public void ExplicitThemesOverrideSystemAndSystemModeTracksIt(ThemeMode mode, bool system, bool expected) =>
        Assert.Equal(expected, ThemeService.ResolveDark(mode, system));

    [Fact]
    public void ExistingConfigsDefaultToSystemAndThemeSurvivesSerialization()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        var config = JsonSerializer.Deserialize<AppConfig>("{\"pollIntervalSeconds\":45}", options)!;
        Assert.Equal(ThemeMode.System, config.Theme);
        config.Theme = ThemeMode.Dark;
        var restored = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config, options), options)!;
        Assert.Equal(ThemeMode.Dark, restored.Theme);
        Assert.Equal(45, restored.PollIntervalSeconds);
    }

    [Fact]
    public void TitleUsesTheBuiltAssemblyVersionWithoutTheCommitSuffix()
    {
        var expected = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
        Assert.Equal($"Maaa Assorted Assistant Arranger v{expected}", ApplicationInfo.WindowTitle);
        Assert.DoesNotContain('+', ApplicationInfo.WindowTitle);
    }
}
