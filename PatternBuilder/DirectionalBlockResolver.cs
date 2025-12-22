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
            { (CardinalDirection.North, "l"), CardinalDirection.East },
            { (CardinalDirection.North, "r"), CardinalDirection.West },

            { (CardinalDirection.East, "f"), CardinalDirection.East },
            { (CardinalDirection.East, "b"), CardinalDirection.West },
            { (CardinalDirection.East, "l"), CardinalDirection.North },
            { (CardinalDirection.East, "r"), CardinalDirection.South },

            { (CardinalDirection.South, "f"), CardinalDirection.South },
            { (CardinalDirection.South, "b"), CardinalDirection.North },
            { (CardinalDirection.South, "l"), CardinalDirection.West },
            { (CardinalDirection.South, "r"), CardinalDirection.East },

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

            if (relativeDirection is "up" or "down")
            {
                return playerDirection;
            }

            if (RelativeToAbsoluteMap.TryGetValue((playerDirection, relativeDirection), out CardinalDirection absoluteDir))
            {
                return absoluteDir;
            }

            return playerDirection;
        }

        public static int? ResolveBlockId(string blockCodeWithDirectives, CardinalDirection playerDirection, ICoreAPI api)
        {
            var directives = ParseDirectives(blockCodeWithDirectives);

            if (directives.RelativeDirection == null && directives.AxisHint == null)
            {
                var baseBlock = api.World.GetBlock(new AssetLocation(directives.BaseBlockCode));
                return baseBlock?.BlockId;
            }

            CardinalDirection absoluteDirection = TranslateRelativeToAbsolute(
                directives.RelativeDirection,
                playerDirection);

            return TryResolveVariant(directives.BaseBlockCode, absoluteDirection, directives, api);
        }

        private static int? TryResolveVariant(string baseCode, CardinalDirection direction, BlockDirectives directives, ICoreAPI api)
        {
            string[] candidates = GenerateVariantCandidates(baseCode, direction, directives);

            foreach (var candidate in candidates)
            {
                var blocks = api.World.SearchBlocks(new AssetLocation(candidate));
                if (blocks != null && blocks.Length > 0)
                {
                    return blocks[0].BlockId;
                }
            }

            var fallbackBlock = api.World.SearchBlocks(new AssetLocation(baseCode + "*"));
            if (fallbackBlock != null && fallbackBlock.Length > 0)
            {
                return fallbackBlock[0].BlockId;
            }

            return null;
        }

        private static string[] GenerateVariantCandidates(string baseCode, CardinalDirection direction, BlockDirectives directives)
        {
            if (directives.AxisHint == "horizontal")
            {
                string axis = (direction == CardinalDirection.North || direction == CardinalDirection.South)
                    ? "ns" : "ew";

                return new[] {
                    $"{baseCode}-{axis}",
                    $"{baseCode}-{axis}-*",
                    $"{baseCode}-{ExpandAxis(axis)}",
                    $"{baseCode}-{ExpandAxis(axis)}-*"
                };
            }

            if (directives.AxisHint == "vertical" || directives.RelativeDirection == "up" || directives.RelativeDirection == "down")
            {
                return new[] {
                    $"{baseCode}-ud",
                    $"{baseCode}-ud-*",
                    $"{baseCode}-up",
                    $"{baseCode}-up-*",
                    $"{baseCode}-down",
                    $"{baseCode}-down-*",
                };
            }

            string dirAbbr = direction.ToString().ToLower()[0].ToString();
            string dirFull = direction.ToString().ToLower();

            return new[] {
                $"{baseCode}-up-{dirFull}-*",
                $"{baseCode}-up-{dirAbbr}-*",
                $"{baseCode}-down-{dirFull}-*",
                $"{baseCode}-down-{dirAbbr}-*",
                $"{baseCode}-*-{dirFull}-*",
                $"{baseCode}-*-{dirAbbr}-*",
                $"{baseCode}-{dirAbbr}",
                $"{baseCode}-{dirAbbr}-*",
                $"{baseCode}-{dirFull}",
                $"{baseCode}-{dirFull}-*",
                $"{baseCode}*"
            };
        }

        private static string ExpandAxis(string axis)
        {
            return axis switch
            {
                "ns" => "northsouth",
                "ew" => "eastwest",
                "ud" => "updown",
                _ => axis
            };
        }
    }
}
