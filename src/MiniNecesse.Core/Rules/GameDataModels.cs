using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Immutable game balance configuration loaded externally from config.json (zero magic numbers in code).
/// </summary>
public sealed class GameConfig
{
    /// <summary>Gets the fixed simulation timestep in seconds (default 1/60s).</summary>
    public float FixedDeltaTime { get; }

    /// <summary>Gets the pixel/world dimension of each square map tile.</summary>
    public float TileSize { get; }

    /// <summary>Gets the duration of daytime in seconds.</summary>
    public float DayDurationSeconds { get; }

    /// <summary>Gets the duration of nighttime in seconds.</summary>
    public float NightDurationSeconds { get; }

    /// <summary>Gets the player's starting maximum health points.</summary>
    public int PlayerBaseHp { get; }

    /// <summary>Gets the player's base movement speed in world units per second.</summary>
    public float PlayerBaseSpeed { get; }

    /// <summary>Gets the player's base unarmed attack/harvest damage.</summary>
    public int PlayerBaseDamage { get; }

    /// <summary>Gets the player's inventory slot capacity.</summary>
    public int InventorySlots { get; }

    /// <summary>Gets the number of quick-select hotbar slots.</summary>
    public int HotbarSlots { get; }

    /// <summary>Gets the interaction radius around a placed workbench in world units.</summary>
    public float WorkbenchInteractRadius { get; }

    /// <summary>Initializes a new instance of the <see cref="GameConfig"/> class.</summary>
    public GameConfig(
        float fixedDeltaTime = 1f / 60f,
        float tileSize = 32f,
        float dayDurationSeconds = 60f,
        float nightDurationSeconds = 45f,
        int playerBaseHp = 100,
        float playerBaseSpeed = 140f,
        int playerBaseDamage = 10,
        int inventorySlots = 20,
        int hotbarSlots = 9,
        float workbenchInteractRadius = 80f)
    {
        FixedDeltaTime = fixedDeltaTime;
        TileSize = tileSize;
        DayDurationSeconds = dayDurationSeconds;
        NightDurationSeconds = nightDurationSeconds;
        PlayerBaseHp = playerBaseHp;
        PlayerBaseSpeed = playerBaseSpeed;
        PlayerBaseDamage = playerBaseDamage;
        InventorySlots = inventorySlots;
        HotbarSlots = hotbarSlots;
        WorkbenchInteractRadius = workbenchInteractRadius;
    }
}

/// <summary>
/// Immutable high-score leaderboard entry persisted in highscore.json.
/// </summary>
public sealed record HighScoreEntry(
    string PlayerName,
    int Score,
    string IslandId,
    string Difficulty,
    DateTime AchievedAtUtc);

/// <summary>
/// Immutable resource placement coordinate loaded from an island JSON definition.
/// </summary>
public sealed record ResourceSpawnPoint(
    ResourceType ResourceType,
    int TileCol,
    int TileRow);

/// <summary>
/// Immutable boss configuration loaded from an island JSON definition.
/// </summary>
public sealed record BossConfiguration(
    string BossType,
    string DisplayName,
    int MaxHp,
    float MoveSpeed,
    int AttackDamage,
    float AttackRange,
    float AttackCooldown,
    int ScoreReward,
    string InitialStrategyKey,
    string EnrageStrategyKey,
    float EnrageThresholdRatio);

/// <summary>
/// Complete loaded island package combining the composed <see cref="IslandLevel"/>
/// with its JSON-configured resources, raid waves, and island boss metadata.
/// </summary>
public sealed class LoadedIslandPackage
{
    /// <summary>Gets the constructed island tilemap.</summary>
    public IslandLevel Level { get; }

    /// <summary>Gets the explicit resource spawn points defined in JSON.</summary>
    public IReadOnlyList<ResourceSpawnPoint> ResourceSpawns { get; }

    /// <summary>Gets the island's raid wave schedule.</summary>
    public IReadOnlyList<WaveDefinition> Waves { get; }

    /// <summary>Gets the island's boss configuration.</summary>
    public BossConfiguration Boss { get; }

    /// <summary>Gets the deterministic seed recorded in the island file.</summary>
    public int Seed { get; }

    /// <summary>Initializes a new instance of the <see cref="LoadedIslandPackage"/> class.</summary>
    public LoadedIslandPackage(
        IslandLevel level,
        IReadOnlyList<ResourceSpawnPoint> resourceSpawns,
        IReadOnlyList<WaveDefinition> waves,
        BossConfiguration boss,
        int seed)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(resourceSpawns);
        ArgumentNullException.ThrowIfNull(waves);
        ArgumentNullException.ThrowIfNull(boss);

        Level = level;
        ResourceSpawns = resourceSpawns;
        Waves = waves;
        Boss = boss;
        Seed = seed;
    }
}
