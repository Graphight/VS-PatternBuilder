using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using PatternBuilder.Config;

namespace PatternBuilder.Inventory;

public class ToolDurabilityManager
{
    public class ToolRequirement
    {
        public int BlockId { get; set; }
        public BlockPos Position { get; set; }
        public int RequiredDurability { get; set; }
        public EnumBlockMaterial BlockMaterial { get; set; }
    }

    public class ToolConsumptionResult
    {
        public bool Success { get; set; }
        public string FailureReason { get; set; }
        public List<string> ToolsSwitched { get; set; } = new List<string>();
        public List<ItemStack> HarvestedItems { get; set; } = new List<ItemStack>();
    }

    public static bool CanToolBreakBlock(CollectibleObject tool, Block block, ICoreAPI api)
    {
        if (tool == null || block == null)
            return false;

        if (tool.Tool == null)
            return false;

        if (block.Code.Path == "air")
            return true;

        if (tool.MiningSpeed != null && tool.MiningSpeed.TryGetValue(block.BlockMaterial, out float speed))
        {
            return speed > 0;
        }

        if (block.RequiredMiningTier > 0 && tool.ToolTier >= block.RequiredMiningTier)
        {
            return true;
        }

        return false;
    }

    public static ItemSlot FindSuitableToolInInventory(IPlayer player, Block block, ICoreAPI api, out bool foundInHotbar)
    {
        foundInHotbar = false;

        if (player?.InventoryManager == null)
            return null;

        ItemSlot hotbarTool = FindToolInInventory(player.InventoryManager.GetHotbarInventory(), block, api);
        if (hotbarTool != null)
        {
            foundInHotbar = true;
            return hotbarTool;
        }

        foreach (var inventory in player.InventoryManager.Inventories.Values)
        {
            if (inventory == player.InventoryManager.GetHotbarInventory())
                continue;

            ItemSlot backpackTool = FindToolInInventory(inventory, block, api);
            if (backpackTool != null)
            {
                foundInHotbar = false;
                return backpackTool;
            }
        }

        return null;
    }

    private static ItemSlot FindToolInInventory(IInventory inventory, Block block, ICoreAPI api)
    {
        if (inventory == null)
            return null;

        foreach (var slot in inventory)
        {
            if (slot == null || slot.Empty || slot.Itemstack == null)
                continue;

            CollectibleObject collectible = slot.Itemstack.Collectible;
            if (collectible == null)
                continue;

            if (CanToolBreakBlock(collectible, block, api))
            {
                int remainingDurability = collectible.GetRemainingDurability(slot.Itemstack);
                if (remainingDurability > 0)
                {
                    return slot;
                }
            }
        }

        return null;
    }

    public static Dictionary<EnumBlockMaterial, int> CalculateDurabilityRequirements(
        List<int> blockIds,
        List<BlockPos> positions,
        ICoreAPI api,
        PatternBuilderConfig config)
    {
        var requirements = new Dictionary<EnumBlockMaterial, int>();

        for (int i = 0; i < blockIds.Count; i++)
        {
            int blockId = blockIds[i];
            Block block = api.World.GetBlock(blockId);

            if (block == null || block.Code.Path == "air")
                continue;

            EnumBlockMaterial material = block.BlockMaterial;

            if (!requirements.ContainsKey(material))
            {
                requirements[material] = 0;
            }

            requirements[material] += config.DurabilityPerBlock;
        }

        return requirements;
    }

    public static bool HasSufficientToolDurability(
        IPlayer player,
        Dictionary<EnumBlockMaterial, int> requirements,
        ICoreAPI api,
        out string missingToolMessage)
    {
        missingToolMessage = null;

        foreach (var requirement in requirements)
        {
            EnumBlockMaterial material = requirement.Key;
            int requiredDurability = requirement.Value;

            int availableDurability = GetTotalDurabilityForMaterial(player, material, api);

            if (availableDurability < requiredDurability)
            {
                missingToolMessage = $"Insufficient tool durability for {material} blocks (need {requiredDurability}, have {availableDurability})";
                return false;
            }
        }

        return true;
    }

    private static int GetTotalDurabilityForMaterial(IPlayer player, EnumBlockMaterial material, ICoreAPI api)
    {
        int totalDurability = 0;

        if (player?.InventoryManager == null)
            return 0;

        foreach (var inventory in player.InventoryManager.Inventories.Values)
        {
            foreach (var slot in inventory)
            {
                if (slot == null || slot.Empty || slot.Itemstack == null)
                    continue;

                CollectibleObject collectible = slot.Itemstack.Collectible;
                if (collectible?.Tool == null)
                    continue;

                if (collectible.MiningSpeed != null &&
                    collectible.MiningSpeed.TryGetValue(material, out float speed) &&
                    speed > 0)
                {
                    totalDurability += collectible.GetRemainingDurability(slot.Itemstack);
                }
            }
        }

        return totalDurability;
    }

    public static ToolConsumptionResult ConsumeToolDurabilityAndHarvestBlocks(
        IPlayer player,
        List<int> blockIds,
        List<BlockPos> positions,
        IWorldAccessor world,
        PatternBuilderConfig config)
    {
        var result = new ToolConsumptionResult { Success = true };

        if (player == null || world == null || blockIds == null || positions == null)
        {
            result.Success = false;
            result.FailureReason = "Invalid parameters";
            return result;
        }

        if (blockIds.Count != positions.Count)
        {
            result.Success = false;
            result.FailureReason = "Block count and position count mismatch";
            return result;
        }

        IBlockAccessor blockAccessor = world.BlockAccessor;
        var materialToTool = new Dictionary<EnumBlockMaterial, ItemSlot>();

        for (int i = 0; i < blockIds.Count; i++)
        {
            int blockId = blockIds[i];
            BlockPos pos = positions[i];

            Block block = world.GetBlock(blockId);
            if (block == null || block.Code.Path == "air")
                continue;

            EnumBlockMaterial material = block.BlockMaterial;

            if (!materialToTool.TryGetValue(material, out ItemSlot toolSlot))
            {
                toolSlot = FindSuitableToolInInventory(player, block, world.Api, out bool inHotbar);
                if (toolSlot == null)
                {
                    result.Success = false;
                    result.FailureReason = $"No suitable tool found for {material} blocks";
                    return result;
                }

                materialToTool[material] = toolSlot;

                if (!inHotbar && !result.ToolsSwitched.Contains(toolSlot.Itemstack.GetName()))
                {
                    result.ToolsSwitched.Add(toolSlot.Itemstack.GetName());
                }
            }

            if (config.HarvestCarvedBlocks)
            {
                Block existingBlock = blockAccessor.GetBlock(pos);
                if (existingBlock != null && existingBlock.Code.Path != "air")
                {
                    ItemStack[] drops = existingBlock.GetDrops(world, pos, player, 1.0f);
                    if (drops != null && drops.Length > 0)
                    {
                        result.HarvestedItems.AddRange(drops);
                    }
                }
            }

            CollectibleObject tool = toolSlot.Itemstack.Collectible;
            tool.DamageItem(world, player.Entity, toolSlot, config.DurabilityPerBlock);

            if (toolSlot.Empty || tool.GetRemainingDurability(toolSlot.Itemstack) <= 0)
            {
                toolSlot.MarkDirty();
                materialToTool.Remove(material);

                ItemSlot newTool = FindSuitableToolInInventory(player, block, world.Api, out bool inHotbar);
                if (newTool == null)
                {
                    result.Success = false;
                    result.FailureReason = $"Tool broke and no replacement found for {material} blocks";
                    return result;
                }

                materialToTool[material] = newTool;

                if (!inHotbar && !result.ToolsSwitched.Contains(newTool.Itemstack.GetName()))
                {
                    result.ToolsSwitched.Add(newTool.Itemstack.GetName());
                }
            }
        }

        foreach (var slot in materialToTool.Values)
        {
            slot.MarkDirty();
        }

        if (config.HarvestCarvedBlocks && result.HarvestedItems.Count > 0)
        {
            AddItemsToInventoryOrDrop(player, result.HarvestedItems, world);
        }

        return result;
    }

    private static void AddItemsToInventoryOrDrop(IPlayer player, List<ItemStack> items, IWorldAccessor world)
    {
        if (player?.InventoryManager == null || items == null || items.Count == 0)
            return;

        foreach (var itemStack in items)
        {
            if (itemStack == null || itemStack.StackSize <= 0)
                continue;

            bool success = player.InventoryManager.TryGiveItemstack(itemStack, true);

            if (!success)
            {
                world.SpawnItemEntity(itemStack, player.Entity.Pos.XYZ);
            }
        }
    }
}
