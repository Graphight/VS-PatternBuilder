using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PatternBuilder.TerrainFollowing;

public enum PatternType
{
    Normal,
    TransitionUp,
    TransitionDown
}

public class TerrainFollowingManager
{
    private readonly ICoreClientAPI api;

    public TerrainFollowingManager(ICoreClientAPI api)
    {
        this.api = api;
    }

    public (BlockPos adjustedPosition, PatternType patternType) GetAdjustedPlacementPosition(
        BlockPos basePosition,
        CardinalDirection direction
    )
    {
        BlockPos lookaheadPos = OffsetPositionForward(basePosition, direction, 1);
        var blockAccessor = api.World.BlockAccessor;
        int? detectedGroundY = TerrainDetector.DetectGroundLevel(lookaheadPos, blockAccessor);

        PatternType patternType = PatternType.Normal;
        int placementY = basePosition.Y;

        if (detectedGroundY.HasValue)
        {
            int delta = detectedGroundY.Value - placementY;

            if (delta > 0)
            {
                placementY += 1;
                patternType = PatternType.TransitionUp;
            }
            else if (delta <= -1)
            {
                patternType = PatternType.TransitionDown;
            }
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