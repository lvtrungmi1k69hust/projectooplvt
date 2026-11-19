using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Serializable data transfer object capturing a complete snapshot of a game session
/// for persistence through <see cref="MiniNecesse.Core.Interfaces.ISaveStorage"/>.
/// </summary>
public sealed class GameStateSnapshot
{
    /// <summary>Gets or sets the save schema version.</summary>
    public string Version { get; set; } = "2026.1";

    /// <summary>Gets or sets the UTC timestamp when the snapshot was saved.</summary>
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the current island identifier.</summary>
    public string IslandId { get; set; } = string.Empty;

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
    public List<EntityStateSnapshot> Structures { get; set; } = new();

    /// <summary>Gets or sets active enemy and boss snapshots.</summary>
    public List<EntityStateSnapshot> Enemies { get; set; } = new();
}

/// <summary>
/// Serializable player state stored inside <see cref="GameStateSnapshot"/>.
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

    /// <summary>Gets or sets the selected hotbar slot index.</summary>
    public int SelectedHotbarIndex { get; set; }

    /// <summary>Gets or sets the equipped weapon upgrade tier.</summary>
    public int WeaponUpgradeLevel { get; set; }

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
/// Serializable world entity record for resources, structures, and enemies.
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
