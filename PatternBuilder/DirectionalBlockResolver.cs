using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PatternBuilder
{
    public class BlockDirectives
    {
        public string BaseBlockCode { get; set; }
        public string RelativeDirection { get; set; }
        public string AxisHint { get; set; }
        public bool IsAuto { get; set; }

        public BlockDirectives(string baseBlockCode)
        {
            BaseBlockCode = baseBlockCode;
            RelativeDirection = null;
            AxisHint = null;
            IsAuto = false;
        }
    }

    public static class DirectionalBlockResolver
    {
        private static readonly Dictionary<(CardinalDirection player, string relative), CardinalDirection> RelativeToAbsoluteMap = new()
        {
            { (CardinalDirection.North, "f"), CardinalDirection.North },
            { (CardinalDirection.North, "b"), CardinalDirection.South },
            { (CardinalDirection.North, "l"), CardinalDirection.West },
            { (CardinalDirection.North, "r"), CardinalDirection.East },

            { (CardinalDirection.East, "f"), CardinalDirection.East },
            { (CardinalDirection.East, "b"), CardinalDirection.West },
            { (CardinalDirection.East, "l"), CardinalDirection.North },
            { (CardinalDirection.East, "r"), CardinalDirection.South },

            { (CardinalDirection.South, "f"), CardinalDirection.South },
            { (CardinalDirection.South, "b"), CardinalDirection.North },
            { (CardinalDirection.South, "l"), CardinalDirection.East },
            { (CardinalDirection.South, "r"), CardinalDirection.West },

            { (CardinalDirection.West, "f"), CardinalDirection.West },
            { (CardinalDirection.West, "b"), CardinalDirection.East },
            { (CardinalDirection.West, "l"), CardinalDirection.South },
            { (CardinalDirection.West, "r"), CardinalDirection.North }
        };

        public static BlockDirectives ParseDirectives(string blockCodeWithDirectives)
        {
            if (string.IsNullOrEmpty(blockCodeWithDirectives))
            {
                return new BlockDirectives("air");
            }

            if (!blockCodeWithDirectives.Contains("|"))
            {
                return new BlockDirectives(blockCodeWithDirectives);
            }

            string[] parts = blockCodeWithDirectives.Split('|');
            string baseCode = parts[0];
            var directives = new BlockDirectives(baseCode);

            for (int i = 1; i < parts.Length; i++)
            {
                string directive = parts[i].ToLower().Trim();

                switch (directive)
                {
                    case "f":
                    case "b":
                    case "l":
                    case "r":
                    case "up":
                    case "down":
                        if (directives.RelativeDirection == null)
                        {
                            directives.RelativeDirection = directive;
                        }
                        break;

                    case "horizontal":
                    case "vertical":
                        if (directives.AxisHint == null)
                        {
                            directives.AxisHint = directive;
                        }
                        break;

                    case "auto":
                        directives.IsAuto = true;
                        break;

                    default:
                        break;
                }
            }

            return directives;
        }

        public static CardinalDirection TranslateRelativeToAbsolute(string relativeDirection, CardinalDirection playerDirection)
        {
            if (string.IsNullOrEmpty(relativeDirection))
            {
                return playerDirection;
            }

            relativeDirection = relativeDirection.ToLower().Trim();

            if (relativeDirection == "up" || relativeDirection == "down")
            {
                return playerDirection;
            }

            if (RelativeToAbsoluteMap.TryGetValue((playerDirection, relativeDirection), out CardinalDirection absoluteDir))
            {
                return absoluteDir;
            }

            return playerDirection;
        }
    }
}
