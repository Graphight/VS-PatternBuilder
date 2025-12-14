using System;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BuilderRoads;

public class BuilderRoadsModSystem : ModSystem
{
    private ICoreClientAPI clientApi;
    private bool roadBuildingEnabled = false;
    private BlockPos lastPlacementPos;
    private long tickListenerId;

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
            player.ShowChatNotification($"Moved {distance:F2} blocks - would place road here!");
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

    public override void Dispose()
    {
        if (clientApi != null)
        {
            clientApi.Event.UnregisterGameTickListener(tickListenerId);
        }
        base.Dispose();
    }
}