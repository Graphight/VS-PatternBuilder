using System;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BuilderRoads;

public enum CardinalDirection
{
    North,
    South,
    East,
    West
}

public class BuilderRoadsModSystem : ModSystem
{
    private ICoreClientAPI clientApi;
    private bool roadBuildingEnabled = false;
    private BlockPos lastPlacementPos;
    private long tickListenerId;

    // Pattern definition: [Y layer][horizontal index]
    private readonly string[,] roadPattern = {
        {"game:soil-medium-normal", "game:soil-medium-normal", "game:soil-medium-normal"},  // Foundation
        {"game:gravel-granite", "game:gravel-granite", "game:gravel-granite"}                // Surface
    };

    // Cached block IDs for performance
    private int foundationBlockId;
    private int surfaceBlockId;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        // Server-side functionality not needed for Phase 1
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.clientApi = api;

        // Cache block IDs for performance
        CacheBlockIds();

        // Register the hotkey: Ctrl+Shift+R
        clientApi.Input.RegisterHotKey("toggleroadbuilder", "Toggle Road Builder",
            GlKeys.R, HotkeyType.CharacterControls,
            ctrlPressed: true, shiftPressed: true);

        // Set up the hotkey handler
        clientApi.Input.SetHotKeyHandler("toggleroadbuilder", OnToggleRoadBuilder);

        // Register position tracking tick listener (200ms = 0.2 seconds)
        tickListenerId = clientApi.Event.RegisterGameTickListener(OnGameTick, 200);

        Mod.Logger.Notification("BuilderRoads loaded - Press Ctrl+Shift+R to toggle");
    }

    private void CacheBlockIds()
    {
        var foundationBlock = clientApi.World.GetBlock(new AssetLocation("game:soil-medium-normal"));
        var surfaceBlock = clientApi.World.GetBlock(new AssetLocation("game:gravel-granite"));

        if (foundationBlock == null || surfaceBlock == null)
        {
            Mod.Logger.Error("BuilderRoads: Failed to load block types!");
            foundationBlockId = 0;
            surfaceBlockId = 0;
        }
        else
        {
            foundationBlockId = foundationBlock.BlockId;
            surfaceBlockId = surfaceBlock.BlockId;
            Mod.Logger.Notification($"BuilderRoads: Cached block IDs - Foundation: {foundationBlockId}, Surface: {surfaceBlockId}");
        }
    }

    private bool OnToggleRoadBuilder(KeyCombination key)
    {
        roadBuildingEnabled = !roadBuildingEnabled;

        string status = roadBuildingEnabled ? "enabled" : "disabled";
        var player = clientApi.World.Player;
        player?.ShowChatNotification($"Road building mode {status}");

        // Reset last placement position when toggling on
        if (roadBuildingEnabled)
        {
            if (player?.Entity != null)
            {
                lastPlacementPos = player.Entity.Pos.AsBlockPos;
            }
        }

        return true;
    }

    private void OnGameTick(float dt)
    {
        // Only track position if road building is enabled
        if (!roadBuildingEnabled)
        {
            return;
        }

        var player = clientApi.World.Player;
        if (player?.Entity == null)
        {
            return;
        }

        BlockPos currentPos = player.Entity.Pos.AsBlockPos;

        // If this is the first tick with road building enabled, initialize
        if (lastPlacementPos == null)
        {
            lastPlacementPos = currentPos.Copy();
            return;
        }

        // Calculate distance moved
        double distance = CalculateDistance(lastPlacementPos, currentPos);

        // Check if we've moved far enough to place a new segment (>0.8 blocks)
        if (distance > 0.8)
        {
            CardinalDirection direction = CalculateDirection(lastPlacementPos, currentPos);

            // Offset placement ahead of player (1 block in movement direction)
            BlockPos placePos = OffsetPositionForward(currentPos, direction, 1);

            // Place the road pattern ahead of player
            PlaceRoadPattern(placePos, direction);

            player.ShowChatNotification($"Placed road segment {direction}");
            lastPlacementPos = currentPos.Copy();
        }
    }

    private double CalculateDistance(BlockPos from, BlockPos to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        int dz = to.Z - from.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private CardinalDirection CalculateDirection(BlockPos from, BlockPos to)
    {
        int dx = to.X - from.X;
        int dz = to.Z - from.Z;

        // Determine which axis has the strongest movement
        if (Math.Abs(dx) > Math.Abs(dz))
        {
            // Moving primarily along X axis
            return dx > 0 ? CardinalDirection.East : CardinalDirection.West;
        }
        else
        {
            // Moving primarily along Z axis
            return dz > 0 ? CardinalDirection.South : CardinalDirection.North;
        }
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

    private void PlaceRoadPattern(BlockPos centerPos, CardinalDirection direction)
    {
        if (foundationBlockId == 0 || surfaceBlockId == 0)
        {
            Mod.Logger.Warning("BuilderRoads: Block IDs not cached, skipping placement");
            return;
        }

        var blockAccessor = clientApi.World.BlockAccessor;
        int patternWidth = 3;
        int patternHeight = 2;

        // Offset Y so the top surface (gravel) is one block below player's feet
        // Pattern: Y-2 = foundation, Y-1 = surface (walkable)
        int baseY = centerPos.Y - 2;

        // Place blocks based on direction
        for (int y = 0; y < patternHeight; y++)
        {
            int blockId = (y == 0) ? foundationBlockId : surfaceBlockId;

            for (int i = 0; i < patternWidth; i++)
            {
                int offset = i - 1;  // -1, 0, 1 (centered)
                BlockPos placePos;

                // Determine block position based on movement direction
                if (direction == CardinalDirection.North || direction == CardinalDirection.South)
                {
                    // Moving N/S, pattern extends along X axis
                    placePos = new BlockPos(centerPos.X + offset, baseY + y, centerPos.Z);
                }
                else
                {
                    // Moving E/W, pattern extends along Z axis
                    placePos = new BlockPos(centerPos.X, baseY + y, centerPos.Z + offset);
                }

                // Place the block
                blockAccessor.SetBlock(blockId, placePos);
            }
        }
    }

    public override void Dispose()
    {
        if (clientApi != null)
        {
            clientApi.Event.UnregisterGameTickListener(tickListenerId);
        }
        base.Dispose();
    }
}