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

            var validationErrors = pattern.GetValidationErrors(api);
            if (validationErrors.Count > 0)
            {
                api.Logger.Error($"PatternBuilder: Pattern '{pattern.Name}' validation failed:");
                foreach (var error in validationErrors)
                {
                    api.Logger.Error($"  - {error}");
                }
                return null;
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

                return int.TryParse(afterSlot.Substring(0, i), out slotNumber) && slotNumber >= 1 && slotNumber <= PatternManager.MaxSlots;
            }
        }

        return int.TryParse(afterSlot, out slotNumber) && slotNumber >= 1 && slotNumber <= PatternManager.MaxSlots;
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
                    Slices = [ "DDD,GGG,_P_,___" ],
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
                FileName = "slot2_lamppost_road.json",
                Pattern = new PatternDefinition
                {
                    Name = "Lamp Post Road",
                    Description = "3-wide gravel road with lamp posts every 8 blocks",
                    Slices = [
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,_P_,___,___",
                        "DDD,GGG,WPW,W_W,L_L"
                    ],
                    Width = 3,
                    Height = 5,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'D', "game:soil-medium-normal" },
                        { 'G', "game:gravel-granite" },
                        { 'W', "game:woodenfence-oak-empty-free" },
                        { 'L', "game:paperlantern-on" },
                        { 'P', "player" }
                    }
                }
            },
            new
            {
                FileName = "slot3_wildcard_road.json",
                Pattern = new PatternDefinition
                {
                    Name = "Wildcard Road",
                    Description = "5-wide road using any soil/gravel variants (wildcard demo)",
                    Slices = [ "SSSSS,GGGGG,__P__,_____" ],
                    Width = 5,
                    Height = 4,
                    Blocks = new Dictionary<char, string>
                    {
                        { 'S', "game:soil-*" },
                        { 'G', "game:gravel-*" },
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
                    Slices = [ "CSS,CP_,C__,C__" ],
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
                    Slices = [ "SSSSS,S_P_S,S___S,S___S,SSSSS" ],
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
- Files must be named 'slotN_name.json' where N is 1-50
- Example: slot1_road.json, slot2_lamppost_road.json

2D Pattern Format (single slice):
{
  ""name"": ""Simple Road"",
  ""description"": ""3-wide gravel road"",
  ""slices"": [ ""DDD,GGG,_P_,___"" ],
  ""width"": 3,
  ""height"": 4,
  ""mode"": ""adaptive"",
  ""blocks"": {
    ""D"": ""game:soil-medium-normal"",
    ""G"": ""game:gravel-granite"",
    ""P"": ""player""
  }
}

3D Pattern Format (multiple slices):
{
  ""name"": ""Lamp Post Road"",
  ""description"": ""Road with lamp posts every 8 blocks"",
  ""slices"": [
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,_P_,___"",
    ""DDD,GGG,LPL,_L_""
  ],
  ""width"": 3,
  ""height"": 4,
  ""blocks"": {
    ""D"": ""game:soil-medium-normal"",
    ""G"": ""game:gravel-granite"",
    ""L"": ""game:lantern-iron-on"",
    ""P"": ""player""
  }
}

Slice Format:
- Each slice is a 2D pattern (comma-separated rows, bottom to top Y-layers)
- Each character represents one block
- '_' = air/empty
- 'P' = player's feet position (required in each slice)
- Walk forward to progress through slices, backward to reverse

Mode:
- ""adaptive"" (default): Only places solid blocks, preserves existing terrain
- ""carve"": Places air blocks to cut through terrain (for tunnels)

Block Codes:
- Use standard VS AssetLocation format: ""game:blockname""
- Find block codes in VS creative menu or wiki
- Wildcards supported: ""game:soil-*"" matches any soil variant

Tips:
- Player marker 'P' determines vertical offset in each slice
- Width/height must match each slice's dimensions
- Use mode=""carve"" for tunnels that need to cut through terrain
- Walk forward/backward to move through 3D pattern slices
- Turn left/right to keep current slice (no increment/decrement)
- Edit files while game is running, use '.pb reload' to apply changes
";
            File.WriteAllText(readmePath, readme);
            api.Logger.Notification("PatternBuilder: Created README.txt");
        }
    }
}