using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Patterns.Factory;

namespace MiniNecesse.Core.Tests;

public sealed class CraftingSystemTests
{
    [Fact]
    public void TryCraft_WithRequiredMaterials_ConsumesIngredientsAndAddsItemFromFactory()
    {
        var itemFactory = ItemFactory.CreateDefault();
        var craftingSystem = new CraftingSystem(itemFactory.ResolveItem);
        var inventory = new Inventory(capacity: 10);

        inventory.TryAddItem(itemFactory.ResolveItem("wood"), 4);
        inventory.TryAddItem(itemFactory.ResolveItem("copper_ore"), 8);

        var swordRecipe = new CraftingRecipe(
            "craft_copper_sword",
            "copper_sword",
            outputQuantity: 1,
            requiresWorkbench: true,
            new[]
            {
                new RecipeIngredient("wood", 2),
                new RecipeIngredient("copper_ore", 6)
            });

        bool crafted = craftingSystem.TryCraft(inventory, swordRecipe, nearWorkbench: true, out ItemStack? result);

        Assert.True(crafted);
        Assert.NotNull(result);
        Assert.Equal("copper_sword", result!.Item.Id);
        Assert.Equal(1, inventory.GetItemCount("copper_sword"));
        Assert.Equal(2, inventory.GetItemCount("wood"));
        Assert.Equal(2, inventory.GetItemCount("copper_ore"));
    }

    [Fact]
    public void CraftOrThrow_WhenMaterialsMissing_ThrowsInsufficientMaterialExceptionAndPreservesInventory()
    {
        var itemFactory = ItemFactory.CreateDefault();
        var craftingSystem = new CraftingSystem(itemFactory.ResolveItem);
        var inventory = new Inventory(capacity: 10);
        inventory.TryAddItem(itemFactory.ResolveItem("wood"), 3);

        var workbenchRecipe = new CraftingRecipe(
            "craft_workbench",
            "workbench",
            outputQuantity: 1,
            requiresWorkbench: false,
            new[] { new RecipeIngredient("wood", 6) });

        InsufficientMaterialException ex = Assert.Throws<InsufficientMaterialException>(
            () => craftingSystem.CraftOrThrow(inventory, workbenchRecipe, nearWorkbench: false));

        Assert.Equal("wood", ex.MissingItemId);
        Assert.Equal(6, ex.RequiredQuantity);
        Assert.Equal(3, ex.AvailableQuantity);
        Assert.Equal(3, inventory.GetItemCount("wood"));
        Assert.Equal(0, inventory.GetItemCount("workbench"));
    }

    [Fact]
    public void TryCraft_WhenWorkbenchRequiredAndNotNearby_ReturnsFalseAndPreservesInventory()
    {
        var itemFactory = ItemFactory.CreateDefault();
        var craftingSystem = new CraftingSystem(itemFactory.ResolveItem);
        var inventory = new Inventory(capacity: 10);
        inventory.TryAddItem(itemFactory.ResolveItem("wood"), 10);
        inventory.TryAddItem(itemFactory.ResolveItem("iron_ore"), 10);

        var ironSwordRecipe = new CraftingRecipe(
            "craft_iron_sword",
            "iron_sword",
            outputQuantity: 1,
            requiresWorkbench: true,
            new[]
            {
                new RecipeIngredient("wood", 2),
                new RecipeIngredient("iron_ore", 6)
            });

        bool crafted = craftingSystem.TryCraft(inventory, ironSwordRecipe, nearWorkbench: false, out ItemStack? result);

        Assert.False(crafted);
        Assert.Null(result);
        Assert.Equal(10, inventory.GetItemCount("wood"));
        Assert.Equal(10, inventory.GetItemCount("iron_ore"));
        Assert.Equal(0, inventory.GetItemCount("iron_sword"));
    }
}
