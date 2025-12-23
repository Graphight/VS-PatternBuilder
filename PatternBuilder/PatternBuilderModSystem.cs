using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PatternBuilder.Config;
using PatternBuilder.Inventory;
using PatternBuilder.Networking;
using PatternBuilder.Pattern;
using PatternBuilder.Preview;
using PatternBuilder.TerrainFollowing;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
    private const double NormalPlacementThreshold = 0.6;
    private const int NormalTickIntervalMs = 100;
    private const int FastTickIntervalMs = 50;

    private ICoreClientAPI clientApi;
    private ICoreServerAPI serverApi;
    private bool buildingEnabled = false;
    private BlockPos lastPlacementPos;
    private CardinalDirection? lastDirection;
    private CardinalDirection? forwardDirection;
    private PatternType? lastPlacedPatternType;
    private long tickListenerId;
    private int currentTickInterval = NormalTickIntervalMs;

    private PatternManager patternManager;
    private PatternLoader patternLoader;
    private Dictionary<string, int> blockIdCache;
    private PatternBuilderConfig config;

    private PreviewRenderer previewRenderer;
    private PreviewManager previewManager;
    private TerrainFollowingManager terrainFollowingManager;

    private IClientNetworkChannel clientChannel;
    private IServerNetworkChannel serverChannel;

    private int cachedInventoryChecks = 0;
    private const int InventoryCheckCacheSize = 5;

    private const string NetworkChannelId = "patternbuilder";

    private static readonly Dictionary<CardinalDirection, CardinalDirection> OppositeDirections = new()
    {
        { CardinalDirection.North, CardinalDirection.South },
        { CardinalDirection.South, CardinalDirection.North },
        { CardinalDirection.East, CardinalDirection.West },
        { CardinalDirection.West, CardinalDirection.East }
    };

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

        var configPath = Path.Combine(api.GetOrCreateDataPath("ModConfig"), "patternbuilder");
        config = PatternBuilderConfig.Load(api, configPath);
        config.Validate(api);

        serverChannel = api.Network.GetChannel(NetworkChannelId)
            .SetMessageHandler<PlacePatternMessage>(OnClientPlacePattern);

        Mod.Logger.Notification("PatternBuilder server-side loaded");
    }

    private void OnClientPlacePattern(IPlayer fromPlayer, PlacePatternMessage message)
    {
        if (message.BlockIds.Count != message.Positions.Count)
        {
            Mod.Logger.Warning($"PatternBuilder: Mismatched block counts from {fromPlayer.PlayerName}");
            return;
        }

        bool isCreative = InventoryHelper.IsCreativeMode(fromPlayer);

        if (!isCreative && message.RequiredPatterns != null && message.RequiredPatterns.Count > 0)
        {
            if (!InventoryHelper.ConsumeBlocksFromInventory(fromPlayer, message.RequiredPatterns, serverApi))
            {
                Mod.Logger.Warning($"PatternBuilder: Server-side consumption failed for {fromPlayer.PlayerName}");
                return;
            }
        }

        if (!isCreative && message.RequiresToolDurability)
        {
            Mod.Logger.Notification($"PatternBuilder: Consuming tool durability for {fromPlayer.PlayerName} (GameMode: {fromPlayer.WorldData.CurrentGameMode})");

            var positions = message.Positions.Select(p => p.ToBlockPos()).ToList();
            var result = ToolDurabilityManager.ConsumeToolDurabilityAndHarvestBlocks(
                fromPlayer,
                message.BlockIds,
                positions,
                serverApi.World,
                config
            );

            if (!result.Success)
            {
                Mod.Logger.Warning($"PatternBuilder: Tool durability consumption failed for {fromPlayer.PlayerName}: {result.FailureReason}");
                ((ICoreServerAPI)serverApi).SendMessage(fromPlayer, 0, $"Pattern building failed: {result.FailureReason}", EnumChatType.Notification);
                return;
            }

            if (result.ToolsSwitched != null && result.ToolsSwitched.Count > 0)
            {
                foreach (var toolName in result.ToolsSwitched)
                {
                    ((ICoreServerAPI)serverApi).SendMessage(fromPlayer, 0, $"Switched to {toolName} from backpack", EnumChatType.Notification);
                }
            }

            if (result.HarvestedItems != null && result.HarvestedItems.Count > 0)
            {
                Mod.Logger.Notification($"PatternBuilder: Harvested {result.HarvestedItems.Sum(i => i.StackSize)} items for {fromPlayer.PlayerName}");
            }
        }

        var blockAccessor = serverApi.World.BlockAccessor;

        for (int i = 0; i < message.BlockIds.Count; i++)
        {
            int blockId = message.BlockIds[i];
            BlockPos pos = message.Positions[i].ToBlockPos();

            blockAccessor.SetBlock(blockId, pos);
        }

        if (message.AutoConnectPositions != null && message.AutoConnectPositions.Count > 0)
        {
            foreach (var serializedPos in message.AutoConnectPositions)
            {
                BlockPos pos = serializedPos.ToBlockPos();
                blockAccessor.TriggerNeighbourBlockUpdate(pos);
            }
        }
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.clientApi = api;

        var configPath = Path.Combine(api.GetOrCreateDataPath("ModConfig"), "patternbuilder");
        config = PatternBuilderConfig.Load(api, configPath);
        config.Validate(api);

        clientChannel = api.Network.GetChannel(NetworkChannelId);

        patternManager = new PatternManager(api);
        patternLoader = new PatternLoader(api);

        LoadPatterns(createDefaults: true);

        blockIdCache = new Dictionary<string, int>();
        CacheBlockIdsForPattern(patternManager.GetCurrentPattern());

        previewRenderer = new PreviewRenderer(api);
        api.Event.RegisterRenderer(previewRenderer, EnumRenderStage.Opaque, "patternbuilder_preview");

        previewManager = new PreviewManager(api, previewRenderer, patternManager, blockIdCache);
        terrainFollowingManager = new TerrainFollowingManager(api);

        RegisterCommands(api);

        tickListenerId = clientApi.Event.RegisterGameTickListener(OnGameTick, NormalTickIntervalMs);

        Mod.Logger.Notification("PatternBuilder loaded - Use .pb help for commands");
    }

    private void LoadPatterns(bool createDefaults = false)
    {
        var configPath = Path.Combine(clientApi.GetOrCreateDataPath("ModConfig"), "patternbuilder", "patterns");

        if (createDefaults)
        {
            patternLoader.CreateDefaultPatterns(configPath);
        }

        var loadedPatterns = patternLoader.LoadAllPatterns(configPath);

        patternManager.LoadPatterns(loadedPatterns);

        foreach (var pattern in loadedPatterns.Values)
        {
            var validationErrors = pattern.GetValidationErrors(clientApi);
            if (validationErrors.Count > 0)
            {
                foreach (var error in validationErrors)
                {
                    Mod.Logger.Warning($"Pattern '{pattern.Name}': {error}");
                }
                clientApi.ShowChatMessage($"Pattern '{pattern.Name}' has issues - check logs or pattern file");
            }
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
            .BeginSubCommand("info")
                .WithDescription("Show current pattern details")
                .HandleWith(OnCommandInfo)
            .EndSubCommand()
            .BeginSubCommand("slot")
                .WithDescription($"Switch to pattern slot (1-{PatternManager.MaxSlots})")
                .WithArgs(api.ChatCommands.Parsers.Int("slot"))
                .HandleWith(OnCommandSlot)
            .EndSubCommand()
            .BeginSubCommand("preview")
                .WithDescription("Toggle pattern preview on/off")
                .HandleWith(OnCommandPreview)
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
        SetBuilding(true);
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandOff(TextCommandCallingArgs args)
    {
        SetBuilding(false);
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
        for (int i = 1; i <= PatternManager.MaxSlots; i++)
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

    private TextCommandResult OnCommandInfo(TextCommandCallingArgs args)
    {
        var pattern = patternManager.GetCurrentPattern();

        int currentSlot = patternManager.GetCurrentSlot();
        int currentSlice = patternManager.GetCurrentSliceIndex();
        int depth = pattern.GetDepth();

        clientApi.ShowChatMessage($"Current Pattern [Slot {currentSlot}]:");
        clientApi.ShowChatMessage($"  Name: {pattern.Name}");
        clientApi.ShowChatMessage($"  Description: {pattern.Description}");
        clientApi.ShowChatMessage($"  Dimensions: {pattern.Width}x{pattern.Height} (Depth: {depth})");
        clientApi.ShowChatMessage($"  Current Slice: {currentSlice + 1}/{depth}");
        clientApi.ShowChatMessage($"  Mode: {pattern.Mode ?? "adaptive"}");

        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandHelp(TextCommandCallingArgs args)
    {
        clientApi.ShowChatMessage("PatternBuilder Commands:");
        clientApi.ShowChatMessage("  .pb help - Show command help");
        clientApi.ShowChatMessage("  .pb toggle - Toggle pattern building on/off");
        clientApi.ShowChatMessage("  .pb on - Enable pattern building");
        clientApi.ShowChatMessage("  .pb off - Disable pattern building");
        clientApi.ShowChatMessage("  .pb slot 'X' - Switch to pattern at slot 'X'");
        clientApi.ShowChatMessage("  .pb list - Show available patterns");
        clientApi.ShowChatMessage("  .pb info - Show current pattern details");
        clientApi.ShowChatMessage("  .pb reload - Reload patterns from disk");
        clientApi.ShowChatMessage("  .pb preview - Toggle pattern preview on/off");
        clientApi.ShowChatMessage("");
        clientApi.ShowChatMessage("Walk forward while pattern building is enabled to place patterns");
        return TextCommandResult.Success();
    }

    private TextCommandResult OnCommandSlot(TextCommandCallingArgs args)
    {
        int slot = (int)args.Parsers[0].GetValue();

        if (slot < 1 || slot > PatternManager.MaxSlots)
        {
            clientApi.ShowChatMessage($"Slot must be between 1 and {PatternManager.MaxSlots}");
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

    private TextCommandResult OnCommandPreview(TextCommandCallingArgs args)
    {
        previewManager.TogglePreview();
        string status = previewManager.IsPreviewEnabled ? "enabled" : "disabled";
        clientApi.ShowChatMessage($"Pattern preview {status}");
        return TextCommandResult.Success();
    }

    private void ToggleRoadBuilding()
    {
        SetBuilding(!buildingEnabled);
    }

    private void SetBuilding(bool enabled)
    {
        buildingEnabled = enabled;

        string status = buildingEnabled ? "enabled" : "disabled";
        clientApi.ShowChatMessage($"Pattern building mode {status}");

        if (buildingEnabled)
        {
            var player = clientApi.World.Player;
            if (player?.Entity != null)
            {
                lastPlacementPos = player.Entity.Pos.AsBlockPos;
            }
        }
        else
        {
            lastDirection = null;
            forwardDirection = null;
            lastPlacedPatternType = null;
            previewManager.ClearPreview();
        }
    }

    private void CacheBlockIdsForPattern(PatternDefinition pattern)
    {
        int cachedCount = 0;

        cachedCount += CacheBlocksFromDictionary(pattern.Blocks);

        if (pattern.TransitionUpLayer != null)
        {
            cachedCount += CacheBlocksFromDictionary(pattern.TransitionUpLayer.Blocks);
        }

        if (pattern.TransitionDownLayer != null)
        {
            cachedCount += CacheBlocksFromDictionary(pattern.TransitionDownLayer.Blocks);
        }

        if (pattern.Mode == "carve" && !blockIdCache.ContainsKey("air"))
        {
            var airBlock = clientApi.World.GetBlock(new AssetLocation("game:air"));
            if (airBlock != null)
            {
                blockIdCache["air"] = airBlock.BlockId;
                cachedCount++;
            }
            else
            {
                Mod.Logger.Warning("PatternBuilder: Failed to get air block for carve mode!");
            }
        }
    }

    private int CacheBlocksFromDictionary(Dictionary<char, string> blocks)
    {
        int count = 0;
        foreach (var kvp in blocks)
        {
            string blockCode = kvp.Value;

            if (blockCode == "player" || blockCode == "air")
            {
                continue;
            }

            if (blockCode.Contains("|"))
            {
                continue;
            }

            if (blockIdCache.ContainsKey(blockCode))
                continue;

            var block = clientApi.World.GetBlock(new AssetLocation(blockCode));
            if (block != null)
            {
                blockIdCache[blockCode] = block.BlockId;
                count++;
            }
            else
            {
                Mod.Logger.Warning($"PatternBuilder: Failed to load block '{blockCode}'");
            }
        }
        return count;
    }


    private void OnGameTick(float dt)
    {
        // Only track position if road building is enabled
        if (!buildingEnabled)
        {
            return;
        }

        var player = clientApi.World.Player;
        if (player?.Entity == null)
        {
            return;
        }

        BlockPos currentPos = player.Entity.Pos.AsBlockPos;

        // Determine current direction based on last movement or player facing
        CardinalDirection direction;
        if (forwardDirection.HasValue)
        {
            direction = forwardDirection.Value;
        }
        else
        {
            // Default to North if no direction established yet
            direction = CardinalDirection.North;
        }

        // Calculate distance moved
        double distance = CalculateDistance(lastPlacementPos, currentPos);

        // Peek at movement direction to determine if we're descending
        direction = CalculateDirection(lastPlacementPos, currentPos);
        BlockPos placePos = OffsetPositionForward(currentPos, direction, 1);

        var basePattern = patternManager.GetCurrentPattern();
        bool isCarveMode = basePattern.Mode == "carve";

        PatternType peekPatternType = PatternType.Normal;

        if (!isCarveMode)
        {
            // Peek at terrain to determine pattern type
            var (_, patternTypePeek) = terrainFollowingManager.GetAdjustedPlacementPosition(
                placePos,
                direction
            );
            peekPatternType = patternTypePeek;

            // Adjust tick rate based on terrain type (faster polling for descending)
            AdjustTickRateForPatternType(peekPatternType);
        }

        // Option B: Hybrid approach for descending stairs
        // First descending stair uses distance threshold, subsequent use Y-change
        bool shouldPlace = false;
        if (!isCarveMode && peekPatternType == PatternType.TransitionDown)
        {
            if (lastPlacedPatternType != PatternType.TransitionDown)
            {
                // First descending stair: use normal distance threshold
                shouldPlace = distance > NormalPlacementThreshold;
            }
            else
            {
                // Subsequent descending stairs: only place when Y actually decreases
                shouldPlace = currentPos.Y < lastPlacementPos.Y;
            }
        }
        else
        {
            // For ascending/flat/carve mode, use normal distance threshold
            shouldPlace = distance > NormalPlacementThreshold;
        }

        if (shouldPlace)
        {
            if (!forwardDirection.HasValue)
            {
                forwardDirection = direction;
            }
            else
            {
                UpdateSliceIndexBasedOnDirection(forwardDirection.Value, direction);
            }

            BlockPos adjustedPlacePos;
            PatternType patternType;
            PatternDefinition patternToUse;

            if (isCarveMode)
            {
                // Carve mode: no terrain following, use original position and base pattern
                adjustedPlacePos = placePos;
                patternType = PatternType.Normal;
                patternToUse = basePattern;
            }
            else
            {
                // PHASE 3: Adjust placement Y and determine pattern type (terrain following with transitions)
                var (adjustedPos, pType) = terrainFollowingManager.GetAdjustedPlacementPosition(
                    placePos,
                    direction
                );
                adjustedPlacePos = adjustedPos;
                patternType = pType;

                // Select pattern based on terrain
                patternToUse = patternType switch
                {
                    PatternType.TransitionUp => basePattern.TransitionUpLayer ?? basePattern,
                    PatternType.TransitionDown => basePattern.TransitionDownLayer ?? basePattern,
                    _ => basePattern
                };
            }

            // Place the pattern at adjusted elevation
            PlaceRoadPattern(adjustedPlacePos, direction, patternToUse);

            lastDirection = direction;
            lastPlacementPos = currentPos.Copy();
            lastPlacedPatternType = patternType;
        }

        // Always update preview when building is enabled (2 blocks ahead)
        BlockPos previewPos = OffsetPositionForward(currentPos, direction, 2);
        previewManager.UpdatePreview(previewPos, direction);
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

    private void UpdateSliceIndexBasedOnDirection(CardinalDirection forward, CardinalDirection current)
    {
        if (forward == current)
        {
            patternManager.IncrementSliceIndex();
        }
        else if (OppositeDirections[forward] == current)
        {
            patternManager.DecrementSliceIndex();
        }
        else
        {
            forwardDirection = current;
        }
    }

    private void AdjustTickRateForPatternType(PatternType patternType)
    {
        int targetInterval = patternType == PatternType.TransitionDown ? FastTickIntervalMs : NormalTickIntervalMs;

        if (currentTickInterval != targetInterval)
        {
            clientApi.Event.UnregisterGameTickListener(tickListenerId);
            tickListenerId = clientApi.Event.RegisterGameTickListener(OnGameTick, targetInterval);
            currentTickInterval = targetInterval;
        }
    }

    private void PlaceRoadPattern(BlockPos centerPos, CardinalDirection direction, PatternDefinition currentPattern)
    {

        var player = clientApi.World.Player;
        if (player == null)
        {
            Mod.Logger.Warning("PatternBuilder: Player not found, skipping placement");
            return;
        }

        bool isCreative = InventoryHelper.IsCreativeMode(player);
        Dictionary<string, int> resolvedBlockIds = null;

        if (!isCreative)
        {
            if (cachedInventoryChecks <= 0)
            {
                var requiredPatterns = InventoryHelper.CountBlocksInPattern(currentPattern);
                var availableBlocks = InventoryHelper.CountBlocksInInventory(player, clientApi);

                if (!InventoryHelper.HasSufficientBlocks(requiredPatterns, availableBlocks, clientApi))
                {
                    var missingBlocks = InventoryHelper.GetMissingBlocksDescription(requiredPatterns, availableBlocks, clientApi);

                    if (missingBlocks.Count > 0)
                    {
                        string missingList = string.Join(", ", missingBlocks);
                        clientApi.ShowChatMessage($"Insufficient materials! Need: {missingList}");
                        clientApi.ShowChatMessage("Pattern building auto-disabled. Use '.pb on' to re-enable.");
                        buildingEnabled = false;
                        Mod.Logger.Notification($"PatternBuilder: Auto-disabled - missing materials: {missingList}");
                    }

                    return;
                }

                resolvedBlockIds = InventoryHelper.ResolvePatternToBlockIds(requiredPatterns, availableBlocks, clientApi);
                cachedInventoryChecks = InventoryCheckCacheSize;
            }
            else
            {
                var requiredPatterns = InventoryHelper.CountBlocksInPattern(currentPattern);
                var availableBlocks = InventoryHelper.CountBlocksInInventory(player, clientApi);
                resolvedBlockIds = InventoryHelper.ResolvePatternToBlockIds(requiredPatterns, availableBlocks, clientApi);
                cachedInventoryChecks--;
            }
        }

        int patternWidth = currentPattern.Width;
        int patternHeight = currentPattern.Height;

        int currentSliceIndex = patternManager.GetCurrentSliceIndex();
        if (!currentPattern.ParsePattern(currentSliceIndex))
        {
            Mod.Logger.Error($"PatternBuilder: Failed to parse slice {currentSliceIndex}");
            return;
        }

        var (playerX, playerY) = currentPattern.FindPlayerPosition(currentSliceIndex);
        int baseY = centerPos.Y - playerY;

        var message = new PlacePatternMessage
        {
            PlayerId = player.PlayerUID,
            RequiredPatterns = new Dictionary<string, int>()
        };

        bool isCarveMode = currentPattern.Mode == "carve";
        var blockAccessor = clientApi.World.BlockAccessor;
        var actualBlocksToPlace = new Dictionary<string, int>();

        for (int y = 0; y < patternHeight; y++)
        {
            for (int x = 0; x < patternWidth; x++)
            {
                string blockCode = currentPattern.GetBlockAt(x, y);

                if (blockCode == "air" && !isCarveMode)
                    continue;

                int blockId;

                if (blockCode.Contains("|"))
                {
                    var resolvedId = DirectionalBlockResolver.ResolveBlockId(blockCode, direction, clientApi);
                    if (resolvedId.HasValue)
                    {
                        blockId = resolvedId.Value;
                    }
                    else
                    {
                        Mod.Logger.Warning($"PatternBuilder: Failed to resolve directional block '{blockCode}'");
                        continue;
                    }
                }
                else if (resolvedBlockIds != null && resolvedBlockIds.TryGetValue(blockCode, out int resolvedIdFromInventory))
                {
                    blockId = resolvedIdFromInventory;
                }
                else if (!blockIdCache.TryGetValue(blockCode, out blockId))
                {
                    Mod.Logger.Warning($"PatternBuilder: Block '{blockCode}' not in cache or resolved");
                    continue;
                }

                int offset = x - playerX;
                BlockPos placePos;

                if (direction == CardinalDirection.North || direction == CardinalDirection.South)
                {
                    placePos = new BlockPos(centerPos.X + offset, baseY + y, centerPos.Z);
                }
                else
                {
                    placePos = new BlockPos(centerPos.X, baseY + y, centerPos.Z + offset);
                }

                var existingBlock = blockAccessor.GetBlock(placePos);
                if (existingBlock != null && existingBlock.BlockId == blockId)
                {
                    continue;
                }

                bool shouldAutoConnect = DirectionalBlockResolver.ShouldAutoConnect(blockCode);
                message.AddBlock(blockId, placePos, shouldAutoConnect);

                if (!isCreative && blockCode != "air")
                {
                    if (actualBlocksToPlace.ContainsKey(blockCode))
                        actualBlocksToPlace[blockCode]++;
                    else
                        actualBlocksToPlace[blockCode] = 1;
                }
            }
        }

        if (!isCreative)
        {
            message.RequiredPatterns = actualBlocksToPlace;

            if (isCarveMode && config.RequireToolsForCarving)
            {
                var durabilityRequirements = ToolDurabilityManager.CalculateDurabilityRequirements(
                    message.BlockIds,
                    message.Positions.Select(p => p.ToBlockPos()).ToList(),
                    clientApi,
                    config
                );

                if (!ToolDurabilityManager.HasSufficientToolDurability(
                    player,
                    durabilityRequirements,
                    clientApi,
                    out string missingToolMessage))
                {
                    clientApi.ShowChatMessage($"Insufficient tool durability! {missingToolMessage}");
                    clientApi.ShowChatMessage("Pattern building auto-disabled. Use '.pb on' to re-enable.");
                    buildingEnabled = false;
                    Mod.Logger.Notification($"PatternBuilder: Auto-disabled - {missingToolMessage}");
                    return;
                }

                message.RequiresToolDurability = true;
                message.ShouldHarvestBlocks = config.HarvestCarvedBlocks;
            }
        }

        clientChannel.SendPacket(message);
    }

    public override void Dispose()
    {
        if (clientApi != null)
        {
            clientApi.Event.UnregisterGameTickListener(tickListenerId);
            clientApi.Event.UnregisterRenderer(previewRenderer, EnumRenderStage.Done);
            previewRenderer?.Dispose();
        }
        base.Dispose();
    }
}