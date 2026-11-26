using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Serializable Data Transfer Object capturing 100% of an active game session
/// for JSON persistence through <see cref="MiniNecesse.Core.Interfaces.ISaveStorage"/>.
/// </summary>
public sealed class GameStateDTO
{
    /// <summary>Gets or sets the save schema version.</summary>
    public string Version { get; set; } = "2026.1";

    /// <summary>Gets or sets the UTC timestamp when the snapshot was saved.</summary>
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the current island identifier.</summary>
    public string IslandId { get; set; } = string.Empty;

    /// <summary>Gets or sets the world mode name ("Campaign" or "Endless").</summary>
    public string GameMode { get; set; } = "Campaign";

    /// <summary>Gets or sets the deterministic world seed used for Endless generation (0 for default Campaign).</summary>
    public int WorldSeed { get; set; }

    /// <summary>Gets or sets the difficulty preset name.</summary>
    public string Difficulty { get; set; } = "Normal";

    /// <summary>Gets or sets elapsed gameplay seconds in this run.</summary>
    public double PlayTimeSeconds { get; set; }

    /// <summary>Gets or sets completed tutorial checklist step count.</summary>
    public int TutorialStepIndex { get; set; }

    /// <summary>Gets or sets active weather condition name.</summary>
    public string Weather { get; set; } = "Clear";

    /// <summary>Gets or sets normalized active weather intensity in [0, 1].</summary>
    public float WeatherIntensity { get; set; }

    /// <summary>Gets or sets the accumulated player score.</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets the current in-game day number.</summary>
    public int DayNumber { get; set; } = 1;

    /// <summary>Gets or sets elapsed seconds in the current day/night cycle.</summary>
    public float TimeOfDaySeconds { get; set; }

    /// <summary>Gets or sets the active raid wave index.</summary>
    public int CurrentWaveIndex { get; set; }

    /// <summary>Gets or sets whether the island boss has already spawned.</summary>
    public bool BossSpawned { get; set; }

    /// <summary>Gets or sets the player state snapshot.</summary>
    public PlayerSnapshot Player { get; set; } = new();

    /// <summary>Gets or sets remaining resource node snapshots.</summary>
    public List<EntityStateSnapshot> Resources { get; set; } = new();

    /// <summary>Gets or sets placed structure snapshots.</summary>
    public List<StructureStateSnapshot> Structures { get; set; } = new();

    /// <summary>Gets or sets living enemy and boss snapshots.</summary>
    public List<EnemyStateSnapshot> Enemies { get; set; } = new();

    /// <summary>Gets or sets living passive animal snapshots.</summary>
    public List<AnimalStateSnapshot> Animals { get; set; } = new();

    /// <summary>Gets or sets chunk coordinates whose natural resources have already been generated.</summary>
    public List<ChunkCoordSnapshot> GeneratedChunks { get; set; } = new();

    /// <summary>Gets or sets tilled farm plot snapshots and planted crop progress.</summary>
    public List<FarmPlotSnapshot> FarmPlots { get; set; } = new();

    /// <summary>Gets or sets recruited colony settler NPC snapshots.</summary>
    public List<SettlerStateSnapshot> Settlers { get; set; } = new();

    /// <summary>Gets or sets tracked survival quest progress entries.</summary>
    public List<QuestProgressSnapshot> Quests { get; set; } = new();
}

/// <summary>
/// Serializable progress entry for a single survival quest inside <see cref="GameStateDTO"/>.
/// </summary>
public sealed class QuestProgressSnapshot
{
    public string QuestId { get; set; } = string.Empty;
    public int CurrentCount { get; set; }
}

/// <summary>
/// Serializable recruited settler state stored inside <see cref="GameStateDTO"/>.
/// </summary>
public sealed class SettlerStateSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProfessionId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public int Hp { get; set; }
    public int Happiness { get; set; }
    public bool HasHome { get; set; }
    public int HomeTileCol { get; set; }
    public int HomeTileRow { get; set; }
    public float WorkTimer { get; set; }
    public List<InventoryItemSnapshot>? Stockpile { get; set; }
}

/// <summary>
/// Serializable tilled farmland tile and crop growth state.
/// </summary>
public sealed class FarmPlotSnapshot
{
    public int TileCol { get; set; }
    public int TileRow { get; set; }
    public string? CropId { get; set; }
    public float GrowthSeconds { get; set; }
}

/// <summary>
/// Serializable record for living passive wildlife and livestock animals.
/// </summary>
public sealed class AnimalStateSnapshot
{
    /// <summary>Gets or sets the unique animal identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the animal species name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the world X coordinate.</summary>
    public float X { get; set; }

    /// <summary>Gets or sets the world Y coordinate.</summary>
    public float Y { get; set; }

    /// <summary>Gets or sets the animal's remaining health.</summary>
    public int Hp { get; set; }
}

/// <summary>
/// Serializable chunk coordinate entry tracking visited/generated chunks across save/load.
/// </summary>
public sealed class ChunkCoordSnapshot
{
    /// <summary>Gets or sets the 0-based horizontal chunk index.</summary>
    public int ChunkX { get; set; }

    /// <summary>Gets or sets the 0-based vertical chunk index.</summary>
    public int ChunkY { get; set; }
}

/// <summary>
/// Serializable player state stored inside <see cref="GameStateDTO"/>.
/// </summary>
public sealed class PlayerSnapshot
{
    /// <summary>Gets or sets the player's world X coordinate.</summary>
    public float X { get; set; }

    /// <summary>Gets or sets the player's world Y coordinate.</summary>
    public float Y { get; set; }

    /// <summary>Gets or sets the player's current health.</summary>
    public int Hp { get; set; }

    /// <summary>Gets or sets the player's maximum health.</summary>
    public int MaxHp { get; set; }

    /// <summary>Gets or sets the player's current hunger.</summary>
    public float Hunger { get; set; } = 100f;

    /// <summary>Gets or sets the player's maximum hunger.</summary>
    public float MaxHunger { get; set; } = 100f;

    /// <summary>Gets or sets the selected hotbar slot index.</summary>
    public int SelectedHotbarIndex { get; set; }

    /// <summary>Gets or sets the equipped weapon upgrade tier.</summary>
    public int WeaponUpgradeLevel { get; set; }

    /// <summary>Gets or sets equipped Head armor item id.</summary>
    public string? HeadArmorItemId { get; set; }

    /// <summary>Gets or sets equipped Body armor item id.</summary>
    public string? BodyArmorItemId { get; set; }

    /// <summary>Gets or sets equipped Legs armor item id.</summary>
    public string? LegsArmorItemId { get; set; }

    /// <summary>Gets or sets the 9 pinned hotbar item identifiers (slots 0..2 weapons only).</summary>
    public List<string?>? HotbarItemIds { get; set; }

    /// <summary>Gets or sets the serialized inventory stacks.</summary>
    public List<InventoryItemSnapshot> Inventory { get; set; } = new();
}

/// <summary>
/// Serializable inventory item entry stored inside <see cref="PlayerSnapshot"/>.
/// </summary>
public sealed class InventoryItemSnapshot
{
    /// <summary>Gets or sets the item identifier.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Gets or sets the stack quantity.</summary>
    public int Quantity { get; set; }
}

/// <summary>
/// Serializable world entity record for harvestable resource nodes.
/// </summary>
public sealed class EntityStateSnapshot
{
    /// <summary>Gets or sets the unique entity identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the entity archetype name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the world X coordinate.</summary>
    public float X { get; set; }

    /// <summary>Gets or sets the world Y coordinate.</summary>
    public float Y { get; set; }

    /// <summary>Gets or sets the entity's remaining health.</summary>
    public int Hp { get; set; }
}

/// <summary>
/// Serializable record for player-built structures including grid coordinates.
/// </summary>
public sealed class StructureStateSnapshot
{
    /// <summary>Gets or sets the unique structure identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the structure type name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the world X coordinate.</summary>
    public float X { get; set; }

    /// <summary>Gets or sets the world Y coordinate.</summary>
    public float Y { get; set; }

    /// <summary>Gets or sets the occupied grid column.</summary>
    public int TileCol { get; set; }

    /// <summary>Gets or sets the occupied grid row.</summary>
    public int TileRow { get; set; }

    /// <summary>Gets or sets the structure's remaining health.</summary>
    public int Hp { get; set; }

    /// <summary>Gets or sets stored item stacks if this structure is a container chest.</summary>
    public List<InventoryItemSnapshot>? StoredItems { get; set; }
}

/// <summary>
/// Serializable record for living enemies and bosses.
/// </summary>
public sealed class EnemyStateSnapshot
{
    /// <summary>Gets or sets the unique enemy identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the enemy or boss archetype identifier.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the world X coordinate.</summary>
    public float X { get; set; }

    /// <summary>Gets or sets the world Y coordinate.</summary>
    public float Y { get; set; }

    /// <summary>Gets or sets the enemy's remaining health.</summary>
    public int Hp { get; set; }

    /// <summary>Gets or sets whether this enemy is an island boss.</summary>
    public bool IsBoss { get; set; }
}
