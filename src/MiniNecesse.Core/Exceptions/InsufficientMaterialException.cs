using System;

namespace MiniNecesse.Core.Exceptions;

/// <summary>
/// Exception thrown when an explicit crafting operation fails because
/// the player lacks required ingredients or a nearby workbench.
/// </summary>
public sealed class InsufficientMaterialException : Exception
{
    /// <summary>Gets the identifier of the recipe that could not be crafted.</summary>
    public string RecipeId { get; }

    /// <summary>Gets the identifier of the missing ingredient, if applicable.</summary>
    public string? MissingItemId { get; }

    /// <summary>Gets the required quantity of the missing ingredient.</summary>
    public int RequiredQuantity { get; }

    /// <summary>Gets the available quantity currently in the inventory.</summary>
    public int AvailableQuantity { get; }

    /// <summary>Initializes a new instance of <see cref="InsufficientMaterialException"/>.</summary>
    public InsufficientMaterialException(
        string recipeId,
        string message,
        string? missingItemId = null,
        int requiredQuantity = 0,
        int availableQuantity = 0)
        : base(message)
    {
        RecipeId = recipeId;
        MissingItemId = missingItemId;
        RequiredQuantity = requiredQuantity;
        AvailableQuantity = availableQuantity;
    }
}
