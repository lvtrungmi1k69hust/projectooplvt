namespace MiniNecesse.Core.Enums;

/// <summary>
/// Categorizes items for inventory organization, crafting, and hotbar interaction.
/// </summary>
public enum ItemCategory
{
    /// <summary>Raw or processed crafting material such as wood, stone, or ore.</summary>
    Material = 0,

    /// <summary>Harvesting tool such as an axe or pickaxe.</summary>
    Tool = 1,

    /// <summary>Melee or ranged combat weapon.</summary>
    Weapon = 2,

    /// <summary>Single-use consumable item such as a health potion.</summary>
    Consumable = 3,

    /// <summary>Placeable building structure such as a workbench or wall.</summary>
    Structure = 4
}
