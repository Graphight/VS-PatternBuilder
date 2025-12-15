using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace PatternBuilder;

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
                api.Logger.Warning($"PatternBuilder: Pattern file not found: {filePath}");
                return null;
            }

            string json = File.ReadAllText(filePath);
            var pattern = JsonConvert.DeserializeObject<PatternDefinition>(json);

            if (pattern == null)
            {
                api.Logger.Error($"PatternBuilder: Failed to deserialize pattern: {filePath}");
                return null;
            }

            if (string.IsNullOrEmpty(pattern.Mode))
            {
                pattern.Mode = "adaptive";
            }

            if (!pattern.ValidatePattern())
            {
                api.Logger.Error($"PatternBuilder: Pattern validation failed: {filePath}");
                return null;
            }

            var validationErrors = pattern.GetValidationErrors(api);
            if (validationErrors.Count > 0)
            {
                api.Logger.Warning($"PatternBuilder: Pattern '{pattern.Name}' has validation warnings:");
                foreach (var error in validationErrors)
                {
                    api.Logger.Warning($"  - {error}");
                }
            }

            api.Logger.Notification($"PatternBuilder: Loaded pattern '{pattern.Name}' from {Path.GetFileName(filePath)}");
            return pattern;
        }
        catch (Exception ex)
        {
            api.Logger.Error($"PatternBuilder: Error loading pattern {filePath}: {ex.Message}");
            return null;
        }
    }

    public Dictionary<int, PatternDefinition> LoadAllPatterns(string directory)
    {
        var patterns = new Dictionary<int, PatternDefinition>();

        if (!Directory.Exists(directory))
        {
            api.Logger.Warning($"PatternBuilder: Pattern directory not found: {directory}");
            return patterns;
        }

        var files = Directory.GetFiles(directory, "*.json");
        api.Logger.Notification($"PatternBuilder: Found {files.Length} pattern files in {directory}");

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);

            if (TryParseSlotNumber(fileName, out int slotNumber))
            {
                var pattern = LoadPattern(file);
                if (pattern != null)
                {
                    patterns[slotNumber] = pattern;
                    api.Logger.Notification($"PatternBuilder: Assigned '{pattern.Name}' to slot {slotNumber}");
                }
            }
            else
            {
                api.Logger.Warning($"PatternBuilder: Skipping file '{fileName}' - filename must start with 'slot' followed by number (e.g., slot1_road.json)");
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
            api.Logger.Notification($"PatternBuilder: Created pattern directory: {directory}");
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
                    Pattern = "CSS,CP_,C__,C__",
                    Width = 3,
                    Height = 4,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'C', "game:stonebricks-granite" },
                        { 'S', "game:soil-medium-normal" },
                        { 'P', "player" }
                    }
                }
            },
            new
            {
                FileName = "slot5_stone_tunnel.json",
                Pattern = new PatternDefinition
                {
                    Name = "Default Tunnel",
                    Description = "5-wide stone brick tunnel",
                    Pattern = "SSSSS,S_P_S,S___S,S___S,SSSSS",
                    Width = 5,
                    Height = 5,
                    Mode = "carve",
                    Blocks = new Dictionary<char, string>
                    {
                        { 'S', "game:stonebricks-granite" },
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
                api.Logger.Notification($"PatternBuilder: Created default pattern: {def.FileName}");
            }
        }

        var readmePath = Path.Combine(directory, "README.txt");
        if (!File.Exists(readmePath))
        {
            var readme = @"PatternBuilder Pattern Files
============================

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
  ""mode"": ""adaptive"",
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

Mode:
- ""adaptive"" (default): Adapts to terrain, only places solid blocks, preserves existing terrain
- ""carve"": Carves through terrain, places air blocks to clear space (for tunnels)

Block Codes:
- Use standard VS AssetLocation format: ""game:blockname""
- Find block codes in VS creative menu or wiki

Tips:
- Player marker 'P' determines vertical offset
- Pattern width must match each row's character count
- Pattern height must match number of comma-separated rows
- Use mode=""carve"" for tunnels that need to cut through terrain
- Edit files while game is running, reload world to apply changes
";
            File.WriteAllText(readmePath, readme);
            api.Logger.Notification("PatternBuilder: Created README.txt");
        }
    }
}