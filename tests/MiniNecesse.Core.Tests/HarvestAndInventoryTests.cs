using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Tests;

public sealed class HarvestAndInventoryTests
{
    [Fact]
    public void Harvest_WithMatchingToolUntilDepleted_ReducesHpAndSpawnsDroppedItem()
    {
        var ruleEngine = new RuleEngine();
        var tree = new ResourceNode(
            "tree_1",
            ResourceType.Tree,
            new Vector2D(96f, 96f),
            maxHp: 30,
            requiredTool: ToolType.Axe,
            dropItemId: "wood",
            dropQuantity: 3);

        DroppedItem? drop1 = ruleEngine.ResolveHarvest(tree, ToolType.Axe, toolPower: 15);
        Assert.Null(drop1);
        Assert.Equal(15, tree.CurrentHp);

        DroppedItem? drop2 = ruleEngine.ResolveHarvest(tree, ToolType.Axe, toolPower: 15);
        Assert.NotNull(drop2);
        Assert.False(tree.IsAlive);
        Assert.Equal("wood", drop2!.ItemId);
        Assert.Equal(3, drop2.Quantity);
        Assert.Equal(RuleEngine.HARVEST_SCORE, ruleEngine.Score);
    }

    [Fact]
    public void TryAddItem_CollectedDropAndMatchingItems_StacksIntoInventory()
    {
        var itemFactory = ItemFactory.CreateDefault();
        var inventory = new Inventory(capacity: 5);
        var ruleEngine = new RuleEngine();
        Item wood = itemFactory.ResolveItem("wood");
        var drop = new DroppedItem("drop_1", "wood", 4, new Vector2D(64f, 64f));

        Assert.True(inventory.TryAddItem(wood, 10));
        Assert.True(ruleEngine.ResolvePickup(drop, inventory, itemFactory.ResolveItem));

        Assert.True(drop.IsCollected);
        Assert.Equal(14, inventory.GetItemCount("wood"));
        Assert.Equal(1, inventory.UsedSlots);
    }

    [Fact]
    public void TryAddItem_WhenInventoryIsFull_ReturnsFalseWithoutCorruptingExistingData()
    {
        var inventory = new Inventory(capacity: 2);
        var smallStackMaterial = new MaterialItem("rare_gem", "Rare Gem", maxStackSize: 5);
        var stone = new MaterialItem("stone", "Stone", maxStackSize: 5);

        Assert.True(inventory.TryAddItem(smallStackMaterial, 5));
        Assert.True(inventory.TryAddItem(stone, 5));

        bool added = inventory.TryAddItem(stone, 1);

        Assert.False(added);
        Assert.Equal(2, inventory.UsedSlots);
        Assert.Equal(5, inventory.GetItemCount("rare_gem"));
        Assert.Equal(5, inventory.GetItemCount("stone"));
    }
}
