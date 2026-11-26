namespace MiniNecesse.Core.Enums;

/// <summary>
/// Defines the natural harvestable resource deposits found on islands.
/// </summary>
public enum ResourceType
{
    /// <summary>Tree harvested with an axe for wood.</summary>
    Tree = 0,

    /// <summary>Stone boulder mined with a pickaxe for stone.</summary>
    StoneRock = 1,

    /// <summary>Copper ore deposit mined with a pickaxe.</summary>
    CopperDeposit = 2,

    /// <summary>Iron ore deposit mined with a pickaxe.</summary>
    IronDeposit = 3,

    /// <summary>Gold ore deposit mined with a pickaxe.</summary>
    GoldDeposit = 4,

    /// <summary>Wild berry bush harvested by hand or tool for berries.</summary>
    BerryBush = 5,

    /// <summary>Ripe wheat crop harvested by hand or tool for wheat.</summary>
    WheatCrop = 6,

    /// <summary>Coal seam mined with a pickaxe for fuel and torches.</summary>
    CoalDeposit = 7,

    /// <summary>Arcane crystal cluster mined with a pickaxe for crystal.</summary>
    CrystalCluster = 8
}
