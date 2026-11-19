namespace MiniNecesse.Core.Enums;

/// <summary>
/// Defines the terrain surface types available on an island tilemap.
/// </summary>
public enum TileType
{
    /// <summary>Standard walkable grass terrain.</summary>
    Grass = 0,

    /// <summary>Coastal sand bordering water.</summary>
    Sand = 1,

    /// <summary>Non-walkable water tile surrounding or inside the island.</summary>
    Water = 2,

    /// <summary>Rocky highland ground.</summary>
    Stone = 3,

    /// <summary>Snow-covered biome terrain.</summary>
    Snow = 4,

    /// <summary>Slow-movement swamp ground.</summary>
    Swamp = 5,

    /// <summary>Dark fortress stone terrain.</summary>
    DarkStone = 6
}
