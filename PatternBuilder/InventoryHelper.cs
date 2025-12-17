using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PatternBuilder;

public static class InventoryHelper
{
    public static bool MatchesBlockPattern(int blockId, string pattern, ICoreAPI api)
    {
        var block = api.World.GetBlock(blockId);
        if (block == null)
            return false;

        string blockCode = block.Code.ToString();

        if (!pattern.Contains("*"))
            return blockCode == pattern;

        string regexPattern = "^" + pattern.Replace("*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(blockCode, regexPattern);
    }

    public static Dictionary<string, int> ResolvePatternToBlockIds(
        Dictionary<string, int> requiredPatterns,
        Dictionary<int, int> availableBlocks,
        ICoreAPI api)
    {
        var resolved = new Dictionary<string, int>();

        foreach (var kvp in requiredPatterns)
        {
            string pattern = kvp.Key;

            if (!pattern.Contains("*"))
            {
                var block = api.World.GetBlock(new AssetLocation(pattern));
                if (block != null)
                {
                    resolved[pattern] = block.BlockId;
                }
                continue;
            }

            foreach (var blockKvp in availableBlocks)
            {
                if (MatchesBlockPattern(blockKvp.Key, pattern, api))
                {
                    resolved[pattern] = blockKvp.Key;
                    break;
                }
            }
        }

        return resolved;
    }

    public static bool IsCreativeMode(IPlayer player)
    {
        if (player?.WorldData == null)
            return false;

        return player.WorldData.CurrentGameMode == EnumGameMode.Creative;
    }

    public static Dictionary<string, int> CountBlocksInPattern(PatternDefinition pattern)
    {
        var blockCounts = new Dictionary<string, int>();

        if (pattern == null || pattern.Blocks == null)
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

                if (blockCounts.ContainsKey(blockCode))
                    blockCounts[blockCode]++;
                else
                    blockCounts[blockCode] = 1;
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

        string[] inventoriesToScan = { "hotbar", "backpack" };

        foreach (var invName in inventoriesToScan)
        {
            var inventory = player.InventoryManager.GetOwnInventory(invName);
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
        Dictionary<string, int> requiredPatterns,
        Dictionary<int, int> availableBlocks,
        ICoreAPI api)
    {
        foreach (var kvp in requiredPatterns)
        {
            string pattern = kvp.Key;
            int requiredCount = kvp.Value;

            int totalAvailable = 0;
            foreach (var blockKvp in availableBlocks)
            {
                if (MatchesBlockPattern(blockKvp.Key, pattern, api))
                {
                    totalAvailable += blockKvp.Value;
                }
            }

            if (totalAvailable < requiredCount)
                return false;
        }

        return true;
    }

    public static List<string> GetMissingBlocksDescription(
        Dictionary<string, int> requiredPatterns,
        Dictionary<int, int> availableBlocks,
        ICoreAPI api)
    {
        var missing = new List<string>();

        foreach (var kvp in requiredPatterns)
        {
            string pattern = kvp.Key;
            int requiredCount = kvp.Value;

            int totalAvailable = 0;
            foreach (var blockKvp in availableBlocks)
            {
                if (MatchesBlockPattern(blockKvp.Key, pattern, api))
                {
                    totalAvailable += blockKvp.Value;
                }
            }

            if (totalAvailable < requiredCount)
            {
                int shortage = requiredCount - totalAvailable;
                string displayName = pattern.Contains("*") ? pattern : pattern;

                var block = api.World.GetBlock(new AssetLocation(pattern.Replace("*", "any")));
                if (block != null)
                {
                    displayName = block.GetPlacedBlockName(api.World, new BlockPos(0, 0, 0));
                    if (pattern.Contains("*"))
                        displayName += " (any variant)";
                }

                missing.Add($"{shortage} {displayName}");
            }
        }

        return missing;
    }

    public static bool ConsumeBlocksFromInventory(
        IPlayer player,
        Dictionary<string, int> toConsumePatterns,
        ICoreAPI api)
    {
        if (player?.InventoryManager == null)
            return false;

        string[] inventoriesToScan = { "hotbar", "backpack" };

        foreach (var kvp in toConsumePatterns)
        {
            string pattern = kvp.Key;
            int neededCount = kvp.Value;
            int consumedSoFar = 0;

            foreach (var invName in inventoriesToScan)
            {
                var inventory = player.InventoryManager.GetOwnInventory(invName);
                if (inventory == null)
                    continue;

                foreach (var slot in inventory)
                {
                    if (slot?.Itemstack?.Block == null || slot.Empty)
                        continue;

                    if (!MatchesBlockPattern(slot.Itemstack.Block.BlockId, pattern, api))
                        continue;

                    int availableInSlot = slot.StackSize;
                    int toTakeFromSlot = Math.Min(availableInSlot, neededCount - consumedSoFar);

                    slot.TakeOut(toTakeFromSlot);
                    slot.MarkDirty();

                    consumedSoFar += toTakeFromSlot;

                    if (consumedSoFar >= neededCount)
                        break;
                }

                if (consumedSoFar >= neededCount)
                    break;
            }

            if (consumedSoFar < neededCount)
            {
                return false;
            }
        }

        return true;
    }
}