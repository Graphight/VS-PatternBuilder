using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

public static class TerrainDetector
{
    private const int MaxRaycastDistance = 50;
    private const int RaycastStartOffset = 20;

    public static int? DetectGroundLevel(BlockPos position, IBlockAccessor blockAccessor)
    {
        int startY = position.Y + RaycastStartOffset;
        int minY = position.Y - MaxRaycastDistance;

        for (int y = startY; y >= minY; y--)
        {
            BlockPos checkPos = new BlockPos(position.X, y, position.Z);
            var block = blockAccessor.GetBlock(checkPos);

            if (block == null)
                continue;

            if (!IsAirOrReplaceable(block))
            {
                return y + 1;
            }
        }

        return null;
    }

    private static bool IsAirOrReplaceable(Block block)
    {
        if (block.BlockId == 0)
            return true;

        if (block.Code.Path == "air")
            return true;

        if (block.Replaceable >= 6000)
            return true;

        return false;
    }
}