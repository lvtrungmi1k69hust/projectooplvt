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
    Torch = 3,

    /// <summary>Solid wooden fence used for village animal pens and perimeter enclosures.</summary>
    WoodenFence = 4,

    /// <summary>Interactive wooden storage chest holding an internal item inventory.</summary>
    Chest = 5,

    /// <summary>Walk-through wooden door that seals enclosed rooms for settler housing.</summary>
    WoodenDoor = 6
}
