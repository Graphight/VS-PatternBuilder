using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

public enum CardinalDirection
{
    North,
    South,
    East,
    West
}

public class PatternBuilderModSystem : ModSystem
{
    private ICoreClientAPI clientApi;
    private ICoreServerAPI serverApi;
    private bool roadBuildingEnabled = false;
    private BlockPos lastPlacementPos;
    private long tickListenerId;

    private PatternManager patternManager;
    private PatternLoader patternLoader;
    private Dictionary<string, int> blockIdCache;

    private IClientNetworkChannel clientChannel;
    private IServerNetworkChannel serverChannel;

    private const string NetworkChannelId = "patternbuilder";

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.Network
            .RegisterChannel(NetworkChannelId)
            .RegisterMessageType(typeof(PlacePatternMessage));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        serverApi = api;

        serverChannel = api.Network.GetChannel(NetworkChannelId)
            .SetMessageHandler<PlacePatternMessage>(OnClientPlacePattern);

        Mod.Logger.Notification("PatternBuilder server-side loaded");
    }

    private void OnClientPlacePattern(IPlayer fromPlayer, PlacePatternMessage message)
    {
        if (message == null || message.BlockIds == null || message.Positions == null)
            return;

        if (message.BlockIds.Count != message.Positions.Count)
        {
            Mod.Logger.Warning($"PatternBuilder: Mismatched block counts from {fromPlayer.PlayerName}");
            return;
        }

        var blockAccessor = serverApi.World.BlockAccessor;

        for (int i = 0; i < message.BlockIds.Count; i++)
        {
            int blockId = message.BlockIds[i];
            BlockPos pos = message.Positions[i].ToBlockPos();

            blockAccessor.SetBlock(blockId, pos);
        }

        Mod.Logger.Debug($"PatternBuilder: Placed {message.BlockIds.Count} blocks for {fromPlayer.PlayerName}");
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.clientApi = api;

        clientChannel = api.Network.GetChannel(NetworkChannelId);

        patternManager = new PatternManager(api);
        patternLoader = new PatternLoader(api);

        LoadPatterns();

        blockIdCache = new Dictionary<string, int>();
        CacheBlockIdsForPattern(patternManager.GetCurrentPattern());

        RegisterCommands(api);

        tickListenerId = clientApi.Event.RegisterGameTickListener(OnGameTick, 100);

        Mod.Logger.Notification("PatternBuilder loaded - Use .pb help for commands");
    }

    private void LoadPatterns()
    {
        var configPath = Path.Combine(clientApi.GetOrCreateDataPath("ModConfig"), "patternbuilder", "patterns");

        patternLoader.CreateDefaultPatterns(configPath);

        var loadedPatterns = patternLoader.LoadAllPatterns(configPath);

        patternManager.LoadPatterns(loadedPatterns);

        var patternNames = patternManager.GetAllPatternNames();
        Mod.Logger.Notification($"PatternBuilder: Available patterns:");
        foreach (var kvp in patternNames)
        {
            Mod.Logger.Notification($"  Slot {kvp.Key}: {kvp.Value}");
        }
    }

    private void RegisterCommands(ICoreClientAPI api)
    {
        api.ChatCommands.Create("pb")
            .WithDescription("Pattern building commands")
            .BeginSubCommand("help")
                .WithDescription("Show command help")
                .HandleWith(OnCommandHelp)
            .EndSubCommand()
            .BeginSubCommand("toggle")
                .WithDescription("Toggle road building mode on/off")
                .HandleWith(OnCommandToggle)
            .EndSubCommand()
            .BeginSubCommand("on")
                .WithDescription("Enable road building mode")
                .HandleWith(OnCommandOn)
            .EndSubCommand()
            .BeginSubCommand("off")
                .WithDescription("Disable road building mode")
                .HandleWith(OnCommandOff)
            .EndSubCommand()
            .BeginSubCommand("list")
                .WithDescription("List available patterns")
                .HandleWith(OnCommandList)
            .EndSubCommand()
            .BeginSubCommand("reload")
                .WithDescription("Reload patterns from JSON files")
                .HandleWith(OnCommandReload)
            .EndSubCommand()
            .BeginSubCommand("slot")
                .WithDescription("Switch to pattern slot (1-5)")
                .WithArgs(api.ChatCommands.Parsers.Int("slot"))
                .HandleWith(OnCommandSlot)
            .EndSubCommand()
            .HandleWith(OnCommandHelp);
    }

    private TextCommandResult OnCommandToggle(TextCommandCallingArgs args)
    {
        ToggleRoadBuilding();
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandOn(TextCommandCallingArgs args)
    {
        SetRoadBuilding(true);
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandOff(TextCommandCallingArgs args)
    {
        SetRoadBuilding(false);
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandList(TextCommandCallingArgs args)
    {
        var patternNames = patternManager.GetAllPatternNames();
        var currentSlot = patternManager.GetCurrentSlot();

        if (patternNames.Count == 0)
        {
            clientApi.ShowChatMessage("No patterns available");
            return TextCommandResult.Success();
        }

        clientApi.ShowChatMessage("Available patterns:");
        for (int i = 1; i <= 5; i++)
        {
            if (patternNames.TryGetValue(i, out string name))
            {
                var active = i == currentSlot ? " (active)" : "";
                clientApi.ShowChatMessage($"  [{i}] {name}{active}");
            }
        }

        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandReload(TextCommandCallingArgs args)
    {
        LoadPatterns();
        CacheBlockIdsForPattern(patternManager.GetCurrentPattern());
        clientApi.ShowChatMessage("Patterns reloaded from disk");
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandHelp(TextCommandCallingArgs args)
    {
        clientApi.ShowChatMessage("PatternBuilder Commands:");
        clientApi.ShowChatMessage("  .pb help - Show command help");
        clientApi.ShowChatMessage("  .pb toggle - Toggle pattern building on/off");
        clientApi.ShowChatMessage("  .pb on - Enable pattern building");
        clientApi.ShowChatMessage("  .pb off - Disable pattern building");
        clientApi.ShowChatMessage("  .pb slot <X> - Switch to pattern at slot <X>");
        clientApi.ShowChatMessage("  .pb list - Show available patterns");
        clientApi.ShowChatMessage("  .pb reload - Reload patterns from disk");
        clientApi.ShowChatMessage("");
        clientApi.ShowChatMessage("Walk forward while pattern building is enabled to place patterns");
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandSlot(TextCommandCallingArgs args)
    {
        int slot = (int)args.Parsers[0].GetValue();

        if (slot < 1 || slot > 5)
        {
            clientApi.ShowChatMessage("Slot must be between 1 and 5");
            return TextCommandResult.Error("Invalid slot number");
        }

        if (!patternManager.HasPatternInSlot(slot))
        {
            clientApi.ShowChatMessage($"No pattern in slot {slot}");
            return TextCommandResult.Error("Pattern not found");
        }

        if (patternManager.SwitchToSlot(slot))
        {
            var pattern = patternManager.GetCurrentPattern();
            CacheBlockIdsForPattern(pattern);
            clientApi.ShowChatMessage($"Switched to: {pattern.Name}");
        }

        return TextCommandResult.Success();
    }

    private void ToggleRoadBuilding()
    {
        SetRoadBuilding(!roadBuildingEnabled);
    }

    private void SetRoadBuilding(bool enabled)
    {
        roadBuildingEnabled = enabled;

        string status = roadBuildingEnabled ? "enabled" : "disabled";
        clientApi.ShowChatMessage($"Pattern building mode {status}");

        if (roadBuildingEnabled)
        {
            var player = clientApi.World.Player;
            if (player?.Entity != null)
            {
                lastPlacementPos = player.Entity.Pos.AsBlockPos;
            }
        }
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
                Mod.Logger.Warning($"PatternBuilder: Failed to load block '{blockCode}'");
            }
        }

        if (pattern.Mode == "carve" && !blockIdCache.ContainsKey("air"))
        {
            var airBlock = clientApi.World.GetBlock(new AssetLocation("game:air"));
            if (airBlock != null)
            {
                blockIdCache["air"] = airBlock.BlockId;
                cachedCount++;
                Mod.Logger.Notification($"PatternBuilder: Cached air block for carve mode (ID: {airBlock.BlockId})");
            }
            else
            {
                Mod.Logger.Warning("PatternBuilder: Failed to get air block for carve mode!");
            }
        }

        Mod.Logger.Notification($"PatternBuilder: Cached {cachedCount} block IDs for pattern '{pattern.Name}' (Mode: {pattern.Mode})");
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

        // Check if we've moved far enough to place a new segment (>0.6 blocks)
        if (distance > 0.6)
        {
            CardinalDirection direction = CalculateDirection(lastPlacementPos, currentPos);

            // Offset placement ahead of player (1 block in movement direction)
            BlockPos placePos = OffsetPositionForward(currentPos, direction, 1);

            // Place the road pattern ahead of player
            PlaceRoadPattern(placePos, direction);

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
        var currentPattern = patternManager.GetCurrentPattern();

        if (currentPattern == null)
        {
            Mod.Logger.Warning("PatternBuilder: No pattern loaded, skipping placement");
            return;
        }

        int patternWidth = currentPattern.Width;
        int patternHeight = currentPattern.Height;

        int playerLayer = currentPattern.FindPlayerFeet();
        int baseY = centerPos.Y - playerLayer;

        var message = new PlacePatternMessage();

        bool isCarveMode = currentPattern.Mode == "carve";

        for (int y = 0; y < patternHeight; y++)
        {
            for (int x = 0; x < patternWidth; x++)
            {
                string blockCode = currentPattern.GetBlockAt(x, y);
                if (blockCode == null)
                    continue;

                if (blockCode == "air" && !isCarveMode)
                    continue;

                if (!blockIdCache.TryGetValue(blockCode, out int blockId))
                {
                    Mod.Logger.Warning($"PatternBuilder: Block '{blockCode}' not in cache");
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

                message.AddBlock(blockId, placePos);
            }
        }

        clientChannel.SendPacket(message);
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