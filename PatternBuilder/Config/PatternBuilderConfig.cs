using System;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace PatternBuilder.Config;

public class PatternBuilderConfig
{
    [JsonProperty("requireToolsForCarving")]
    public bool RequireToolsForCarving { get; set; } = true;

    [JsonProperty("harvestCarvedBlocks")]
    public bool HarvestCarvedBlocks { get; set; } = true;

    [JsonProperty("durabilityPerBlock")]
    public int DurabilityPerBlock { get; set; } = 1;

    private static readonly string ConfigFileName = "patternbuilder.json";

    public static PatternBuilderConfig Load(ICoreAPI api, string configDirectory)
    {
        try
        {
            string configPath = Path.Combine(configDirectory, ConfigFileName);

            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
                api.Logger.Notification($"PatternBuilder: Created config directory: {configDirectory}");
            }

            if (!File.Exists(configPath))
            {
                var defaultConfig = new PatternBuilderConfig();
                defaultConfig.Save(api, configDirectory);
                api.Logger.Notification($"PatternBuilder: Created default config at {configPath}");
                return defaultConfig;
            }

            string json = File.ReadAllText(configPath);
            var config = JsonConvert.DeserializeObject<PatternBuilderConfig>(json);

            if (config == null)
            {
                api.Logger.Warning("PatternBuilder: Failed to deserialize config, using defaults");
                return new PatternBuilderConfig();
            }

            api.Logger.Notification($"PatternBuilder: Loaded config from {configPath}");
            api.Logger.Notification($"  - RequireToolsForCarving: {config.RequireToolsForCarving}");
            api.Logger.Notification($"  - HarvestCarvedBlocks: {config.HarvestCarvedBlocks}");
            api.Logger.Notification($"  - DurabilityPerBlock: {config.DurabilityPerBlock}");

            return config;
        }
        catch (Exception ex)
        {
            api.Logger.Error($"PatternBuilder: Error loading config: {ex.Message}");
            return new PatternBuilderConfig();
        }
    }

    public void Save(ICoreAPI api, string configDirectory)
    {
        try
        {
            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            string configPath = Path.Combine(configDirectory, ConfigFileName);
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(configPath, json);

            api.Logger.Notification($"PatternBuilder: Saved config to {configPath}");
        }
        catch (Exception ex)
        {
            api.Logger.Error($"PatternBuilder: Error saving config: {ex.Message}");
        }
    }

    public void Validate(ICoreAPI api)
    {
        if (DurabilityPerBlock < 0)
        {
            api.Logger.Warning($"PatternBuilder: DurabilityPerBlock cannot be negative, using 1");
            DurabilityPerBlock = 1;
        }

        if (DurabilityPerBlock > 100)
        {
            api.Logger.Warning($"PatternBuilder: DurabilityPerBlock seems excessive ({DurabilityPerBlock}), consider lowering it");
        }
    }
}
