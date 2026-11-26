using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game_Daily_Routine_Launcher;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _configPath;
    private readonly string _appDir;

    public ConfigService()
    {
        _appDir = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(_appDir, "appsettings.Local.json");
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return CreateDefault();

        var json = File.ReadAllText(_configPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        var aiSummaryConfigMigrated = config.AiSummary.MigrateLegacyCommonConfig();
        var legacyTasksImported = LegacyBatchTaskImporter.TryApply(config, _appDir);
        var cleanupDefaultsApplied = TaskProcessCleanupDefaults.Apply(config.Tasks);
        if (legacyTasksImported || aiSummaryConfigMigrated || cleanupDefaultsApplied)
            Save(config);

        return config;
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private AppConfig CreateDefault()
    {
        var config = new AppConfig
        {
            Tasks =
            [
                new GameTaskConfig
                {
                    Id = "maa",
                    Name = "明日方舟 (MAA)",
                },
                new GameTaskConfig
                {
                    Id = "march7th",
                    Name = "崩坏：星穹铁道 (March7th)",
                    DelayBeforeStartMs = 30000
                },
                new GameTaskConfig
                {
                    Id = "zzz",
                    Name = "绝区零 (OneDragon)",
                    ToolArgs = "-o -c",
                    GameProcessName = "ZenlessZoneZero",
                },
                new GameTaskConfig
                {
                    Id = "genshin",
                    Name = "原神 (BetterGI)",
                    ToolPath = "bettergi://startOneDragon",
                    LaunchMode = LaunchMode.Uri,
                    GameProcessName = "YuanShen",
                },
                new GameTaskConfig
                {
                    Id = "maaend",
                    Name = "MaaEnd",
                },
                new GameTaskConfig
                {
                    Id = "ww",
                    Name = "鸣潮 (ok-ww)",
                    ToolArgs = "-t 1 -e",
                    GameProcessName = "Wuthering Waves",
                }
            ]
        };

        LegacyBatchTaskImporter.TryApply(config, _appDir);
        TaskProcessCleanupDefaults.Apply(config.Tasks);
        Save(config);
        return config;
    }
}
