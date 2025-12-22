using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace PatternBuilder;

public class PatternDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Slices { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Mode { get; set; }
    public Dictionary<char, string> Blocks { get; set; }
    public PatternDefinition? TransitionUpLayer { get; set; }
    public PatternDefinition? TransitionDownLayer { get; set; }

    private char[,] parsedGrid;

    public PatternDefinition()
    {
        Blocks = new Dictionary<char, string>();
    }

    public int GetDepth()
    {
        return Slices?.Length ?? 0;
    }

    public bool ParsePattern(int sliceIndex)
    {
        if (sliceIndex < 0 || sliceIndex >= Slices.Length)
            return false;

        string slicePattern = Slices[sliceIndex];
        if (string.IsNullOrEmpty(slicePattern))
            return false;

        string[] layers = slicePattern.Split(',');

        if (layers.Length != Height)
        {
            return false;
        }

        parsedGrid = new char[Height, Width];

        for (int y = 0; y < Height; y++)
        {
            string layer = layers[y];
            if (layer.Length != Width)
            {
                return false;
            }

            for (int x = 0; x < Width; x++)
            {
                parsedGrid[y, x] = layer[x];
            }
        }

        return true;
    }

    public bool ValidatePattern(int sliceIndex)
    {
        if (!ParsePattern(sliceIndex))
            return false;

        bool hasPlayerMarker = false;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char c = parsedGrid[y, x];
                if (c == 'P')
                    hasPlayerMarker = true;

                if (c != '_' && !Blocks.ContainsKey(c))
                {
                    return false;
                }
            }
        }

        return hasPlayerMarker;
    }

    public List<string> GetValidationErrors(ICoreAPI api)
    {
        var errors = new List<string>();

        if (Slices == null || Slices.Length == 0)
        {
            errors.Add("No slices defined in pattern");
            return errors;
        }

        for (int sliceIdx = 0; sliceIdx < Slices.Length; sliceIdx++)
        {
            if (!ParsePattern(sliceIdx))
            {
                errors.Add($"Slice {sliceIdx}: Pattern parsing failed - check dimensions match pattern string");
                continue;
            }

            bool hasPlayerMarker = false;
            var invalidBlocks = new List<string>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    char c = parsedGrid[y, x];
                    if (c == 'P')
                        hasPlayerMarker = true;

                    if (c != '_' && c != 'P' && Blocks.ContainsKey(c))
                    {
                        string blockCode = Blocks[c];

                        if (blockCode.Contains("*"))
                            continue;

                        string baseCode = blockCode.Contains("|")
                            ? blockCode.Split('|')[0]
                            : blockCode;

                        Block block = null;

                        if (blockCode.Contains("|"))
                        {
                            var matchingBlocks = api.World.SearchBlocks(new AssetLocation(baseCode + "*"));
                            if (matchingBlocks != null && matchingBlocks.Length > 0)
                            {
                                block = matchingBlocks[0];
                            }
                        }
                        else
                        {
                            block = api.World.GetBlock(new AssetLocation(baseCode));
                        }

                        if (block == null)
                        {
                            invalidBlocks.Add($"'{c}' -> {baseCode}");
                        }
                    }
                }
            }

            if (!hasPlayerMarker)
            {
                errors.Add($"Slice {sliceIdx}: Missing 'P' (player) marker - pattern needs player position");
            }

            if (invalidBlocks.Count > 0)
            {
                errors.Add($"Slice {sliceIdx}: Invalid block codes: {string.Join(", ", invalidBlocks)}");
            }
        }

        if (TransitionUpLayer != null)
        {
            var transitionErrors = TransitionUpLayer.GetValidationErrors(api);
            foreach (var error in transitionErrors)
            {
                errors.Add($"TransitionUpLayer: {error}");
            }
        }

        if (TransitionDownLayer != null)
        {
            var transitionErrors = TransitionDownLayer.GetValidationErrors(api);
            foreach (var error in transitionErrors)
            {
                errors.Add($"TransitionDownLayer: {error}");
            }
        }

        return errors;
    }

    public int FindPlayerFeet(int sliceIndex)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (parsedGrid[y, x] == 'P')
                    return y;
            }
        }

        return 0;
    }

    public string GetBlockAt(int x, int y)
    {
        if (y < 0 || y >= Height || x < 0 || x >= Width)
            return null;

        char c = parsedGrid[y, x];

        if (c == '_' || c == 'P')
            return "air";

        return Blocks.ContainsKey(c) ? Blocks[c] : null;
    }

    public static PatternDefinition CreateHardcodedDefault()
    {
        return new PatternDefinition
        {
            Name = "Adaptive Road with Stairs",
            Description = "Road that adapts to terrain with stair transitions",
            Slices = ["DDD,CCC,_P_,___"],
            Width = 3,
            Height = 4,
            Mode = "adaptive",
            Blocks = new Dictionary<char, string>()
            {
                { 'D', "game:soil-medium-normal" },
                { 'C', "game:cobblestone-granite" },
                { 'P', "player" }
            },
            TransitionUpLayer = new PatternDefinition
            {
                Slices = ["DDD,SSS,_P_,___"],
                Width = 3,
                Height = 4,
                Mode = "carve",
                Blocks = new Dictionary<char, string>()
                {
                    { 'D', "game:soil-medium-normal" },
                    { 'S', "game:cobblestonestairs-*|up|f" },
                    { 'P', "player" }
                }
            },
            TransitionDownLayer = new PatternDefinition
            {
                Slices = ["DDD,SSS,_P_,___"],
                Width = 3,
                Height = 4,
                Mode = "carve",
                Blocks = new Dictionary<char, string>()
                {
                    { 'D', "game:soil-medium-normal" },
                    { 'S', "game:cobblestonestairs-*|up|b" },
                    { 'P', "player" }
                }
            }
        };
    }
}