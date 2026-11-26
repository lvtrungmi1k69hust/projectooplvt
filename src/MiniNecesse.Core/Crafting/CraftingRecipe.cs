using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Immutable crafting recipe specifying required ingredients, optional workbench proximity, and output item.
/// </summary>
public sealed class CraftingRecipe
{
    private readonly string _recipeId;
    private readonly string _outputItemId;
    private readonly int _outputQuantity;
    private readonly bool _requiresWorkbench;
    private readonly List<RecipeIngredient> _ingredients;
    private readonly IReadOnlyList<RecipeIngredient> _ingredientsView;

    /// <summary>Gets the unique identifier of this recipe.</summary>
    public string RecipeId => _recipeId;

    /// <summary>Gets the identifier of the produced item.</summary>
    public string OutputItemId => _outputItemId;

    /// <summary>Gets the number of output items produced per craft.</summary>
    public int OutputQuantity => _outputQuantity;

    /// <summary>Gets a value indicating whether the player must stand near a workbench to craft this recipe.</summary>
    public bool RequiresWorkbench => _requiresWorkbench;

    /// <summary>Gets a cached read-only list of ingredients consumed by this recipe.</summary>
    public IReadOnlyList<RecipeIngredient> Ingredients => _ingredientsView;

    /// <summary>
    /// Initializes a new instance of the <see cref="CraftingRecipe"/> class.
    /// </summary>
    public CraftingRecipe(
        string recipeId,
        string outputItemId,
        int outputQuantity,
        bool requiresWorkbench,
        IEnumerable<RecipeIngredient> ingredients)
    {
        if (string.IsNullOrWhiteSpace(recipeId))
        {
            throw new ArgumentException("RecipeId cannot be empty.", nameof(recipeId));
        }

        if (string.IsNullOrWhiteSpace(outputItemId))
        {
            throw new ArgumentException("OutputItemId cannot be empty.", nameof(outputItemId));
        }

        if (outputQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputQuantity), "Output quantity must be positive.");
        }

        ArgumentNullException.ThrowIfNull(ingredients);
        _ingredients = new List<RecipeIngredient>(ingredients);
        if (_ingredients.Count == 0)
        {
            throw new ArgumentException("A crafting recipe must declare at least one ingredient.", nameof(ingredients));
        }

        _ingredientsView = _ingredients.AsReadOnly();
        _recipeId = recipeId;
        _outputItemId = outputItemId;
        _outputQuantity = outputQuantity;
        _requiresWorkbench = requiresWorkbench;
    }
}
