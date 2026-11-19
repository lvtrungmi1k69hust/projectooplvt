using System;
using MiniNecesse.Core.Exceptions;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Domain service responsible for validating crafting recipes, verifying workbench proximity,
/// deducting ingredients atomically, and adding crafted items into an <see cref="Inventory"/>.
/// Demonstrates UML Dependency on <see cref="CraftingRecipe"/> and <see cref="Inventory"/>.
/// </summary>
public sealed class CraftingSystem
{
    private readonly Func<string, Item> _itemResolver;

    /// <summary>Raised whenever an item stack is successfully crafted.</summary>
    public event Action<CraftingRecipe, ItemStack>? OnItemCrafted;

    /// <summary>
    /// Initializes a new instance of the <see cref="CraftingSystem"/> class.
    /// </summary>
    /// <param name="itemResolver">Delegate resolving an <see cref="Item"/> definition from its catalog identifier.</param>
    public CraftingSystem(Func<string, Item> itemResolver)
    {
        ArgumentNullException.ThrowIfNull(itemResolver);
        _itemResolver = itemResolver;
    }

    /// <summary>
    /// Determines whether <paramref name="recipe"/> can be crafted using <paramref name="inventory"/>
    /// given the player's workbench proximity.
    /// </summary>
    public bool CanCraft(Inventory inventory, CraftingRecipe recipe, bool nearWorkbench)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipe);

        if (recipe.RequiresWorkbench && !nearWorkbench)
        {
            return false;
        }

        return HasAllIngredients(inventory, recipe);
    }

    /// <summary>
    /// Safely attempts to craft <paramref name="recipe"/> without throwing on missing materials or workbench.
    /// </summary>
    public bool TryCraft(Inventory inventory, CraftingRecipe recipe, bool nearWorkbench, out ItemStack? craftedStack)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipe);

        craftedStack = null;
        if (!CanCraft(inventory, recipe, nearWorkbench))
        {
            return false;
        }

        Item outputItem = _itemResolver(recipe.OutputItemId);
        if (!ConsumeIngredientsAndAddOutput(inventory, recipe, outputItem))
        {
            return false;
        }

        craftedStack = new ItemStack(outputItem, recipe.OutputQuantity);
        OnItemCrafted?.Invoke(recipe, craftedStack);
        return true;
    }

    /// <summary>
    /// Crafts <paramref name="recipe"/> or throws <see cref="InsufficientMaterialException"/> with detailed diagnostics.
    /// </summary>
    /// <exception cref="InsufficientMaterialException">Thrown when workbench proximity or materials are missing.</exception>
    public ItemStack CraftOrThrow(Inventory inventory, CraftingRecipe recipe, bool nearWorkbench)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(recipe);

        ValidateRequirementsOrThrow(inventory, recipe, nearWorkbench);

        Item outputItem = _itemResolver(recipe.OutputItemId);
        if (!ConsumeIngredientsAndAddOutput(inventory, recipe, outputItem))
        {
            throw new InsufficientMaterialException(
                recipe.RecipeId,
                $"Inventory does not have enough free space to store {recipe.OutputQuantity}x '{recipe.OutputItemId}'.");
        }

        var craftedStack = new ItemStack(outputItem, recipe.OutputQuantity);
        OnItemCrafted?.Invoke(recipe, craftedStack);
        return craftedStack;
    }

    private static bool HasAllIngredients(Inventory inventory, CraftingRecipe recipe)
    {
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            RecipeIngredient ingredient = recipe.Ingredients[i];
            if (!inventory.HasItem(ingredient.ItemId, ingredient.Quantity))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateRequirementsOrThrow(Inventory inventory, CraftingRecipe recipe, bool nearWorkbench)
    {
        if (recipe.RequiresWorkbench && !nearWorkbench)
        {
            throw new InsufficientMaterialException(
                recipe.RecipeId,
                $"Recipe '{recipe.RecipeId}' requires standing near a Workbench.");
        }

        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            RecipeIngredient ingredient = recipe.Ingredients[i];
            int available = inventory.GetItemCount(ingredient.ItemId);
            if (available < ingredient.Quantity)
            {
                throw new InsufficientMaterialException(
                    recipe.RecipeId,
                    $"Missing '{ingredient.ItemId}': required {ingredient.Quantity}, available {available}.",
                    ingredient.ItemId,
                    ingredient.Quantity,
                    available);
            }
        }
    }

    private bool ConsumeIngredientsAndAddOutput(Inventory inventory, CraftingRecipe recipe, Item outputItem)
    {
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            RecipeIngredient ingredient = recipe.Ingredients[i];
            inventory.TryRemoveItem(ingredient.ItemId, ingredient.Quantity);
        }

        if (inventory.TryAddItem(outputItem, recipe.OutputQuantity))
        {
            return true;
        }

        RollbackIngredients(inventory, recipe);
        return false;
    }

    private void RollbackIngredients(Inventory inventory, CraftingRecipe recipe)
    {
        for (int i = 0; i < recipe.Ingredients.Count; i++)
        {
            RecipeIngredient ingredient = recipe.Ingredients[i];
            inventory.TryAddItem(_itemResolver(ingredient.ItemId), ingredient.Quantity);
        }
    }
}
