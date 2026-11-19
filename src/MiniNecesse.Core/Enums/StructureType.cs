namespace MiniNecesse.Core.Enums;

/// <summary>
/// Defines player-built structures that can be placed on island tiles.
/// </summary>
public enum StructureType
{
    /// <summary>Solid wooden defensive wall.</summary>
    WoodenWall = 0,

    /// <summary>Reinforced stone defensive wall.</summary>
    StoneWall = 1,

    /// <summary>Crafting station unlocking advanced recipes nearby.</summary>
    Workbench = 2,

    /// <summary>Light source illuminating surrounding tiles at night.</summary>
    Torch = 3
}
