using System;
using System.Collections.Generic;
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

    private PatternDefinition currentPattern;
    private Dictionary<string, int> blockIdCache;

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

        currentPattern = PatternDefinition.CreateHardcodedDefault();
        if (!currentPattern.ValidatePattern())
        {
            Mod.Logger.Error("BuilderRoads: Failed to validate default pattern!");
        }

        blockIdCache = new Dictionary<string, int>();
        CacheBlockIdsForPattern(currentPattern);

        clientApi.Input.RegisterHotKey("toggleroadbuilder", "Toggle Road Builder",
            GlKeys.R, HotkeyType.CharacterControls,
            ctrlPressed: true, shiftPressed: true);

        clientApi.Input.SetHotKeyHandler("toggleroadbuilder", OnToggleRoadBuilder);

        tickListenerId = clientApi.Event.RegisterGameTickListener(OnGameTick, 200);

        Mod.Logger.Notification("BuilderRoads loaded - Press Ctrl+Shift+R to toggle");
    }

    private void CacheBlockIdsForPattern(PatternDefinition pattern)
    {
        if (pattern == null || pattern.Blocks == null)
            return;

        int cachedCount = 0;
        foreach (var kvp in pattern.Blocks)
        {
            string blockCode = kvp.Value;
            if (blockIdCache.ContainsKey(blockCode))
                continue;

            var block = clientApi.World.GetBlock(new AssetLocation(blockCode));
            if (block != null)
            {
                blockIdCache[blockCode] = block.BlockId;
                cachedCount++;
            }
            else
            {
                Mod.Logger.Warning($"BuilderRoads: Failed to load block '{blockCode}'");
            }
        }

        Mod.Logger.Notification($"BuilderRoads: Cached {cachedCount} block IDs for pattern '{pattern.Name}'");
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
        if (currentPattern == null)
        {
            Mod.Logger.Warning("BuilderRoads: No pattern loaded, skipping placement");
            return;
        }

        var blockAccessor = clientApi.World.BlockAccessor;
        int patternWidth = currentPattern.Width;
        int patternHeight = currentPattern.Height;

        int playerLayer = currentPattern.FindPlayerFeet();
        int baseY = centerPos.Y - playerLayer;

        for (int y = 0; y < patternHeight; y++)
        {
            for (int x = 0; x < patternWidth; x++)
            {
                string blockCode = currentPattern.GetBlockAt(x, y);
                if (blockCode == null || blockCode == "air")
                    continue;

                if (!blockIdCache.TryGetValue(blockCode, out int blockId))
                {
                    Mod.Logger.Warning($"BuilderRoads: Block '{blockCode}' not in cache");
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