using System.Collections.Generic;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Data access abstraction (DIP) for loading game balance config, crafting recipes, and island levels from external files.
/// </summary>
public interface ILevelLoader
{
    /// <summary>Loads global game balance configuration from config.json.</summary>
    GameConfig LoadConfig();

    /// <summary>Loads all crafting recipes from recipes.json.</summary>
    IReadOnlyList<CraftingRecipe> LoadRecipes();

    /// <summary>Loads and validates a complete island definition by identifier (e.g., "ISLAND_01" or "island_01").</summary>
    LoadedIslandPackage LoadIsland(string islandId);

    /// <summary>Lists all available island identifiers discovered in DataAssets in progression order.</summary>
    IReadOnlyList<string> GetAvailableIslandIds();
}
