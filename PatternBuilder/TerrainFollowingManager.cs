using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

public enum PatternType
{
    Normal,
    TransitionUp,
    TransitionDown
}

public class TerrainFollowingManager
{
    private readonly ICoreClientAPI api;
    private int? currentElevation;

    public TerrainFollowingManager(ICoreClientAPI api)
    {
        this.api = api;
    }

    public void Initialize(int startingY)
    {
        currentElevation = startingY;
    }

    public void Reset()
    {
        currentElevation = null;
    }

    public (BlockPos adjustedPosition, PatternType patternType) GetAdjustedPlacementPosition(
        BlockPos basePosition,
        CardinalDirection direction,
        out string statusMessage)
    {
        if (!currentElevation.HasValue)
        {
            currentElevation = basePosition.Y;
        }

        BlockPos lookaheadPos = OffsetPositionForward(basePosition, direction, 1);
        var blockAccessor = api.World.BlockAccessor;
        int? detectedGroundY = TerrainDetector.DetectGroundLevel(lookaheadPos, blockAccessor);

        PatternType patternType = PatternType.Normal;
        int placementY = currentElevation.Value;

        if (detectedGroundY.HasValue)
        {
            int delta = detectedGroundY.Value - currentElevation.Value;

            if (delta > 0)
            {
                currentElevation++;
                placementY = currentElevation.Value;
                patternType = PatternType.TransitionUp;
                statusMessage = $"[Terrain] Stepping UP to Y={currentElevation.Value} (target={detectedGroundY.Value}, remaining={delta - 1})";
            }
            else if (delta < -1)
            {
                placementY = currentElevation.Value;
                currentElevation--;
                patternType = PatternType.TransitionDown;
                statusMessage = $"[Terrain] Stepping DOWN from Y={placementY} to Y={currentElevation.Value} (target={detectedGroundY.Value}, remaining={Math.Abs(delta + 1)})";
            }
            else
            {
                statusMessage = $"[Terrain] Maintaining Y={currentElevation.Value} (terrain matched)";
            }
        }
        else
        {
            statusMessage = $"[Terrain] No ground detected, maintaining Y={currentElevation.Value}";
        }

        var adjustedPos = new BlockPos(basePosition.X, placementY, basePosition.Z);
        return (adjustedPos, patternType);
    }

    private BlockPos OffsetPositionForward(BlockPos pos, CardinalDirection direction, int blocks)
    {
        return direction switch
        {
            CardinalDirection.North => new BlockPos(pos.X, pos.Y, pos.Z - blocks),
            CardinalDirection.South => new BlockPos(pos.X, pos.Y, pos.Z + blocks),
            CardinalDirection.East => new BlockPos(pos.X + blocks, pos.Y, pos.Z),
            CardinalDirection.West => new BlockPos(pos.X - blocks, pos.Y, pos.Z),
            _ => pos.Copy()
        };
    }
}