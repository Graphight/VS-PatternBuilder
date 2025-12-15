using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace BuilderRoads;

public class PatternLoader
{
    private readonly ICoreAPI api;

    public PatternLoader(ICoreAPI api)
    {
        this.api = api;
    }

    public PatternDefinition LoadPattern(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                api.Logger.Warning($"BuilderRoads: Pattern file not found: {filePath}");
                return null;
            }

            string json = File.ReadAllText(filePath);
            var pattern = JsonConvert.DeserializeObject<PatternDefinition>(json);

            if (pattern == null)
            {
                api.Logger.Error($"BuilderRoads: Failed to deserialize pattern: {filePath}");
                return null;
            }

            if (!pattern.ValidatePattern())
            {
                api.Logger.Error($"BuilderRoads: Pattern validation failed: {filePath}");
                return null;
            }

            api.Logger.Notification($"BuilderRoads: Loaded pattern '{pattern.Name}' from {Path.GetFileName(filePath)}");
            return pattern;
        }
        catch (Exception ex)
        {
            api.Logger.Error($"BuilderRoads: Error loading pattern {filePath}: {ex.Message}");
            return null;
        }
    }

    public Dictionary<int, PatternDefinition> LoadAllPatterns(string directory)
    {
        var patterns = new Dictionary<int, PatternDefinition>();

        if (!Directory.Exists(directory))
        {
            api.Logger.Warning($"BuilderRoads: Pattern directory not found: {directory}");
            return patterns;
        }

        var files = Directory.GetFiles(directory, "*.json");
        api.Logger.Notification($"BuilderRoads: Found {files.Length} pattern files in {directory}");

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            if (TryParseSlotNumber(fileName, out int slotNumber))
            {
                var pattern = LoadPattern(file);
                if (pattern != null)
                {
                    patterns[slotNumber] = pattern;
                    api.Logger.Notification($"BuilderRoads: Assigned '{pattern.Name}' to slot {slotNumber}");
                }
            }
            else
            {
                api.Logger.Warning($"BuilderRoads: Skipping file '{fileName}' - filename must start with 'slot' followed by number (e.g., slot1_road.json)");
            }
        }

        return patterns;
    }

    private bool TryParseSlotNumber(string fileName, out int slotNumber)
    {
        slotNumber = 0;

        if (!fileName.StartsWith("slot", StringComparison.OrdinalIgnoreCase))
            return false;

        var afterSlot = fileName.Substring(4);

        for (int i = 0; i < afterSlot.Length; i++)
        {
            if (!char.IsDigit(afterSlot[i]))
            {
                if (i == 0)
                    return false;

                return int.TryParse(afterSlot.Substring(0, i), out slotNumber) && slotNumber >= 1 && slotNumber <= 5;
            }
        }

        return int.TryParse(afterSlot, out slotNumber) && slotNumber >= 1 && slotNumber <= 5;
    }

    public void EnsureConfigDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            api.Logger.Notification($"BuilderRoads: Created pattern directory: {directory}");
        }
    }

    public void CreateDefaultPatterns(string directory)
    {
        EnsureConfigDirectory(directory);

        var defaultPatterns = new[]
        {
            new
            {
                FileName = "slot1_default_road.json",
                Pattern = new PatternDefinition
                {
                    Name = "Default Road",
                    Description = "3-wide gravel road with dirt foundation",
                    Pattern = "DDD,GGG,_P_,___",
                    Width = 3,
                    Height = 4,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'D', "game:soil-medium-normal" },
                        { 'G', "game:gravel-granite" },
                        { 'P', "player" }
                    }
                }
            },
            new
            {
                FileName = "slot2_narrow_path.json",
                Pattern = new PatternDefinition
                {
                    Name = "Narrow Path",
                    Description = "Single-block dirt path",
                    Pattern = "D,G,P,_",
                    Width = 1,
                    Height = 4,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'D', "game:soil-medium-normal" },
                        { 'G', "game:gravel-granite" },
                        { 'P', "player" }
                    }
                }
            },
            new
            {
                FileName = "slot3_wide_road.json",
                Pattern = new PatternDefinition
                {
                    Name = "Wide Road",
                    Description = "5-wide gravel road for main thoroughfares",
                    Pattern = "SSSSS,GGGGG,__P__,_____",
                    Width = 5,
                    Height = 4,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'S', "game:soil-medium-normal" },
                        { 'G', "game:gravel-granite" },
                        { 'P', "player" }
                    }
                }
            },
            new
            {
                FileName = "slot4_stone_wall.json",
                Pattern = new PatternDefinition
                {
                    Name = "Stone Wall",
                    Description = "3-block tall cobblestone wall",
                    Pattern = "CS,CP,C_,C_",
                    Width = 2,
                    Height = 4,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'C', "game:stonebricks-granite" },
                        { 'S', "game:soil-medium-normal" },
                        { 'P', "player" }
                    }
                }
            }
        };

        foreach (var def in defaultPatterns)
        {
            var filePath = Path.Combine(directory, def.FileName);

            if (!File.Exists(filePath))
            {
                var json = JsonConvert.SerializeObject(def.Pattern, Formatting.Indented);
                File.WriteAllText(filePath, json);
                api.Logger.Notification($"BuilderRoads: Created default pattern: {def.FileName}");
            }
        }

        var readmePath = Path.Combine(directory, "README.txt");
        if (!File.Exists(readmePath))
        {
            var readme = @"BuilderRoads Pattern Files
===========================

Pattern files use Vintage Story's recipe grid syntax for easy editing.

File Naming:
- Files must be named 'slotN_name.json' where N is 1-5
- Example: slot1_road.json, slot2_path.json

Pattern Format:
{
  ""name"": ""Pattern Name"",
  ""description"": ""Description of the pattern"",
  ""pattern"": ""DDD,GGG,_P_,___"",
  ""width"": 3,
  ""height"": 4,
  ""blocks"": {
    ""D"": ""game:soil-medium-normal"",
    ""G"": ""game:gravel-granite"",
    ""P"": ""player""
  }
}

Pattern String:
- Comma-separated rows (bottom to top Y-layers)
- Each character represents one block
- '_' = air/empty
- 'P' = player's feet position (required, marks where player stands)

Block Codes:
- Use standard VS AssetLocation format: ""game:blockname""
- Find block codes in VS creative menu or wiki

Tips:
- Player marker 'P' determines vertical offset
- Pattern width must match each row's character count
- Pattern height must match number of comma-separated rows
- Edit files while game is running, reload world to apply changes
";
            File.WriteAllText(readmePath, readme);
            api.Logger.Notification("BuilderRoads: Created README.txt");
        }
    }
}