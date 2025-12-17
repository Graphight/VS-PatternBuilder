using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

public static class InventoryHelper
{
    public static bool IsCreativeMode(IPlayer player)
    {
        if (player?.WorldData == null)
            return false;

        return player.WorldData.CurrentGameMode == EnumGameMode.Creative;
    }

    public static Dictionary<int, int> CountBlocksInPattern(
        PatternDefinition pattern,
        Dictionary<string, int> blockIdCache)
    {
        var blockCounts = new Dictionary<int, int>();

        if (pattern == null || blockIdCache == null)
            return blockCounts;

        int patternWidth = pattern.Width;
        int patternHeight = pattern.Height;
        bool isCarveMode = pattern.Mode == "carve";

        for (int y = 0; y < patternHeight; y++)
        {
            for (int x = 0; x < patternWidth; x++)
            {
                string blockCode = pattern.GetBlockAt(x, y);
                if (blockCode == null)
                    continue;

                if (blockCode == "air" && !isCarveMode)
                    continue;

                if (blockCode == "air")
                    continue;

                if (!blockIdCache.TryGetValue(blockCode, out int blockId))
                    continue;

                if (blockCounts.ContainsKey(blockId))
                    blockCounts[blockId]++;
                else
                    blockCounts[blockId] = 1;
            }
        }

        return blockCounts;
    }

    public static Dictionary<int, int> CountBlocksInInventory(IPlayer player, ICoreAPI api = null)
    {
        var blockCounts = new Dictionary<int, int>();

        if (player?.InventoryManager == null)
        {
            api?.Logger.Warning("InventoryHelper: Player InventoryManager is null");
            return blockCounts;
        }

        int totalSlots = 0;
        int totalEmpty = 0;
        int totalBlocks = 0;
        int totalItems = 0;

        foreach (var inventory in player.InventoryManager.Inventories.Values)
        {
            if (inventory == null)
                continue;

            api?.Logger.Notification($"InventoryHelper: Scanning inventory: {inventory.ClassName}");

            foreach (var slot in inventory)
            {
                totalSlots++;

                if (slot == null || slot.Empty)
                {
                    totalEmpty++;
                    continue;
                }

                if (slot.Itemstack == null)
                    continue;

                if (slot.Itemstack.Block != null)
                {
                    totalBlocks++;
                    int blockId = slot.Itemstack.Block.BlockId;
                    int stackSize = slot.StackSize;

                    api?.Logger.Notification($"InventoryHelper: Found block: ID={blockId}, Code={slot.Itemstack.Block.Code}, Stack={stackSize}");

                    if (blockCounts.ContainsKey(blockId))
                        blockCounts[blockId] += stackSize;
                    else
                        blockCounts[blockId] = stackSize;
                }
                else if (slot.Itemstack.Item != null)
                {
                    totalItems++;
                }
            }
        }

        api?.Logger.Notification($"InventoryHelper: Total scanned {totalSlots} slots - {totalEmpty} empty, {totalBlocks} blocks, {totalItems} items");

        return blockCounts;
    }

    public static bool HasSufficientBlocks(
        Dictionary<int, int> required,
        Dictionary<int, int> available)
    {
        foreach (var kvp in required)
        {
            int blockId = kvp.Key;
            int requiredCount = kvp.Value;
            int availableCount = available.GetValueOrDefault(blockId, 0);

            if (availableCount < requiredCount)
                return false;
        }

        return true;
    }

    public static List<string> GetMissingBlocksDescription(
        Dictionary<int, int> required,
        Dictionary<int, int> available,
        ICoreAPI api)
    {
        var missing = new List<string>();

        foreach (var kvp in required)
        {
            int blockId = kvp.Key;
            int requiredCount = kvp.Value;
            int availableCount = available.GetValueOrDefault(blockId, 0);

            if (availableCount < requiredCount)
            {
                int shortage = requiredCount - availableCount;
                var block = api.World.GetBlock(blockId);
                string blockName = block?.GetPlacedBlockName(api.World, new BlockPos(0, 0, 0)) ?? $"Block #{blockId}";
                missing.Add($"{shortage} {blockName}");
            }
        }

        return missing;
    }

    public static bool ConsumeBlocksFromInventory(
        IPlayer player,
        Dictionary<int, int> toConsume,
        ICoreAPI api)
    {
        if (player?.InventoryManager == null)
            return false;

        var inventory = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
        if (inventory == null)
            return false;

        var consumedCounts = new Dictionary<int, int>();

        foreach (var kvp in toConsume)
        {
            int blockId = kvp.Key;
            int neededCount = kvp.Value;
            int consumedSoFar = 0;

            foreach (var slot in inventory)
            {
                if (slot?.Itemstack?.Block == null || slot.Empty)
                    continue;

                if (slot.Itemstack.Block.BlockId != blockId)
                    continue;

                int availableInSlot = slot.StackSize;
                int toTakeFromSlot = Math.Min(availableInSlot, neededCount - consumedSoFar);

                slot.TakeOut(toTakeFromSlot);
                slot.MarkDirty();

                consumedSoFar += toTakeFromSlot;

                if (consumedSoFar >= neededCount)
                    break;
            }

            if (consumedSoFar < neededCount)
            {
                return false;
            }

            consumedCounts[blockId] = consumedSoFar;
        }

        return true;
    }
}