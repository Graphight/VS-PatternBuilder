using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace PatternBuilder;

public class PreviewManager
{
    private ICoreClientAPI api;
    private PreviewRenderer renderer;
    private PatternManager patternManager;
    private Dictionary<string, int> blockIdCache;

    private bool previewEnabled = true;
    private BlockPos? lastPreviewPos = null;
    private CardinalDirection? lastPreviewDirection = null;

    private static readonly int TINT_AIR = ColorUtil.ToRgba(100, 50, 255, 50);        // Green for placing in air
    private static readonly int TINT_REPLACE = ColorUtil.ToRgba(100, 100, 150, 255);  // Blue for replacing blocks
    private static readonly int TINT_SAME = ColorUtil.ToRgba(100, 180, 180, 180);     // Grey for same blocks

    public bool IsPreviewEnabled => previewEnabled;

    public PreviewManager(ICoreClientAPI api, PreviewRenderer renderer, PatternManager patternManager, Dictionary<string, int> blockIdCache)
    {
        this.api = api;
        this.renderer = renderer;
        this.patternManager = patternManager;
        this.blockIdCache = blockIdCache;
    }

    public void SetPreviewEnabled(bool enabled)
    {
        previewEnabled = enabled;

        if (!previewEnabled)
        {
            ClearPreview();
        }
    }

    public void TogglePreview()
    {
        SetPreviewEnabled(!previewEnabled);
    }

    public void UpdatePreview(BlockPos centerPos, CardinalDirection direction, Dictionary<string, int>? resolvedBlockIds = null)
    {
        if (!previewEnabled)
        {
            return;
        }

        if (lastPreviewPos != null && lastPreviewPos.Equals(centerPos) && lastPreviewDirection == direction)
        {
            return;
        }

        lastPreviewPos = centerPos.Copy();
        lastPreviewDirection = direction;

        var currentPattern = patternManager.GetCurrentPattern();
        int currentSliceIndex = patternManager.GetCurrentSliceIndex();

        if (!currentPattern.ParsePattern(currentSliceIndex))
        {
            api.World.Logger.Error($"PreviewManager: Failed to parse slice {currentSliceIndex}");
            ClearPreview();
            return;
        }

        int patternWidth = currentPattern.Width;
        int patternHeight = currentPattern.Height;
        int playerLayer = currentPattern.FindPlayerFeet(currentSliceIndex);
        int baseY = centerPos.Y - playerLayer;

        var previewBlocks = new List<PreviewBlock>();
        var blockAccessor = api.World.BlockAccessor;
        bool isCarveMode = currentPattern.Mode == "carve";

        for (int y = 0; y < patternHeight; y++)
        {
            for (int x = 0; x < patternWidth; x++)
            {
                string blockCode = currentPattern.GetBlockAt(x, y);

                if (blockCode == "air" && !isCarveMode)
                    continue;

                int blockId;
                if (resolvedBlockIds != null && resolvedBlockIds.TryGetValue(blockCode, out int resolvedId))
                {
                    blockId = resolvedId;
                }
                else if (!blockIdCache.TryGetValue(blockCode, out blockId))
                {
                    continue;
                }

                int offset = x - (patternWidth / 2);
                BlockPos placePos;

                if (direction == CardinalDirection.North || direction == CardinalDirection.South)
                {
                    placePos = new BlockPos(centerPos.X + offset, baseY + y, centerPos.Z);
                }
                else
                {
                    placePos = new BlockPos(centerPos.X, baseY + y, centerPos.Z + offset);
                }

                Block block = api.World.GetBlock(blockId);
                if (block == null)
                {
                    api.World.Logger.Warning($"PreviewManager: Block is null for blockId {blockId}, blockCode {blockCode}");
                    continue;
                }

                int tintColor = GetTintColor(placePos, blockId);

                var previewBlock = new PreviewBlock(placePos, block, tintColor, api);
                if (previewBlock.UploadMesh())
                {
                    previewBlocks.Add(previewBlock);
                }
                else
                {
                    previewBlock.Dispose();
                }
            }
        }

        api.World.Logger.Notification($"[PreviewManager] Created {previewBlocks.Count} preview blocks at {centerPos}");
        renderer.SetPreviewBlocks(previewBlocks);
    }

    private int GetTintColor(BlockPos pos, int targetBlockId)
    {
        var blockAccessor = api.World.BlockAccessor;
        var existingBlock = blockAccessor.GetBlock(pos);

        if (existingBlock != null && existingBlock.Id != 0 && existingBlock.Code.Path != "air")
        {
            if (existingBlock.Id == targetBlockId)
            {
                return TINT_SAME;      // Grey for same block
            }

            return TINT_REPLACE;       // Blue for replacing different block
        }

        return TINT_AIR;               // Green for placing in air
    }

    public void ClearPreview()
    {
        renderer.ClearPreviewBlocks();
        lastPreviewPos = null;
        lastPreviewDirection = null;
    }
}
