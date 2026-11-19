using System;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a single required ingredient entry inside a <see cref="CraftingRecipe"/>.
/// </summary>
public sealed class RecipeIngredient
{
    private readonly string _itemId;
    private readonly int _quantity;

    /// <summary>Gets the identifier of the required item.</summary>
    public string ItemId => _itemId;

    /// <summary>Gets the positive quantity required.</summary>
    public int Quantity => _quantity;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeIngredient"/> class.
    /// </summary>
    public RecipeIngredient(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Ingredient itemId cannot be empty.", nameof(itemId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Ingredient quantity must be positive.");
        }

        _itemId = itemId;
        _quantity = quantity;
    }
}
