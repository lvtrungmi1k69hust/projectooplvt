namespace MiniNecesse.Core.Enums;

/// <summary>
/// Identifies the functional tool or weapon archetype used for harvesting or combat.
/// </summary>
public enum ToolType
{
    /// <summary>No specialized tool equipped (bare hands).</summary>
    None = 0,

    /// <summary>Woodcutting axe effective against trees.</summary>
    Axe = 1,

    /// <summary>Mining pickaxe effective against rocks and ore veins.</summary>
    Pickaxe = 2,

    /// <summary>Melee blade weapon.</summary>
    Sword = 3,

    /// <summary>Ranged bow firing projectiles.</summary>
    Bow = 4,

    /// <summary>Farming hoe used to till grass or sand into fertile soil plots.</summary>
    Hoe = 5
}
