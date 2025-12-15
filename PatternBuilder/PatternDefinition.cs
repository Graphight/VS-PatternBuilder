using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace PatternBuilder;

public class PatternDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Pattern { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Mode { get; set; }
    public Dictionary<char, string> Blocks { get; set; }

    private char[,] parsedGrid;

    public PatternDefinition()
    {
        Blocks = new Dictionary<char, string>();
    }

    public bool ParsePattern()
    {
        if (string.IsNullOrEmpty(Pattern))
            return false;

        string[] layers = Pattern.Split(',');

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

    public bool ValidatePattern()
    {
        if (!ParsePattern())
            return false;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                char c = parsedGrid[y, x];
                if (c != '_' && !Blocks.ContainsKey(c))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public int FindPlayerFeet()
    {
        if (parsedGrid == null)
            ParsePattern();

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
        if (parsedGrid == null || y < 0 || y >= Height || x < 0 || x >= Width)
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
        };
    }
}