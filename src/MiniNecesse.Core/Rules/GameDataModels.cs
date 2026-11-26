using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.World;

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

    /// <summary>Gets the width and height of one world chunk in tiles.</summary>
    public int ChunkSize { get; }

    /// <summary>Gets the default view distance radius in chunks.</summary>
    public int DefaultViewDistanceChunks { get; }

    /// <summary>Gets the minimum selectable chunk view distance.</summary>
    public int MinViewDistanceChunks { get; }

    /// <summary>Gets the maximum selectable chunk view distance.</summary>
    public int MaxViewDistanceChunks { get; }

    /// <summary>Gets the player's maximum hunger capacity.</summary>
    public float PlayerMaxHunger { get; }

    /// <summary>Gets hunger drain per second while alive in Gameplay.</summary>
    public float HungerDrainPerSecond { get; }

    /// <summary>Gets seconds between starvation damage ticks when hunger hits 0.</summary>
    public float StarvationDamageIntervalSeconds { get; }

    /// <summary>Gets HP lost per starvation tick.</summary>
    public int StarvationDamageAmount { get; }

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
        float workbenchInteractRadius = 80f,
        int chunkSize = IslandLevel.DEFAULT_CHUNK_SIZE,
        int defaultViewDistanceChunks = ChunkManager.DEFAULT_VIEW_DISTANCE,
        int minViewDistanceChunks = ChunkManager.DEFAULT_MIN_VIEW_DISTANCE,
        int maxViewDistanceChunks = ChunkManager.DEFAULT_MAX_VIEW_DISTANCE,
        float playerMaxHunger = 100f,
        float hungerDrainPerSecond = 0.9f,
        float starvationDamageIntervalSeconds = 2.5f,
        int starvationDamageAmount = 3)
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
        ChunkSize = chunkSize;
        DefaultViewDistanceChunks = defaultViewDistanceChunks;
        MinViewDistanceChunks = minViewDistanceChunks;
        MaxViewDistanceChunks = maxViewDistanceChunks;
        PlayerMaxHunger = playerMaxHunger;
        HungerDrainPerSecond = hungerDrainPerSecond;
        StarvationDamageIntervalSeconds = starvationDamageIntervalSeconds;
        StarvationDamageAmount = starvationDamageAmount;
    }
}

/// <summary>
/// Data-driven food and temporary buff definition loaded from <c>foods.json</c>.
/// </summary>
public sealed record FoodDefinition(
    string Id,
    string Name,
    string Description,
    int MaxStackSize,
    int HealAmount,
    float HungerRestore,
    MiniNecesse.Core.Interfaces.IFoodBuffStrategy? BuffStrategy);

/// <summary>
/// Data-driven protective armor piece definition loaded from <c>armor.json</c>.
/// </summary>
public sealed record ArmorDefinition(
    string Id,
    string Name,
    ArmorSlot Slot,
    int Defense,
    int MaxHpBonus,
    string Description);

/// <summary>
/// Immutable high-score leaderboard entry persisted in highscore.json.
/// </summary>
public sealed record HighScoreEntry(
    string PlayerName,
    int Score,
    string IslandId,
    string Difficulty,
    DateTime AchievedAtUtc,
    string Mode = "Campaign",
    double PlayTimeSeconds = 0.0);

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
/// with its JSON-configured resources, procedural chunk spawner, raid waves, and island boss metadata.
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

    /// <summary>Gets the optional per-chunk procedural resource generator for lazy-streamed islands.</summary>
    public Action<ChunkCoord, List<ResourceSpawnPoint>>? ChunkResourceGenerator { get; }

    /// <summary>Gets the optional per-chunk procedural structure generator (e.g., village buildings &amp; fences).</summary>
    public Action<ChunkCoord, List<StructureSpawnPoint>>? ChunkStructureGenerator { get; }

    /// <summary>Gets the optional per-chunk procedural passive animal generator (livestock &amp; wildlife).</summary>
    public Action<ChunkCoord, List<AnimalSpawnPoint>>? ChunkAnimalGenerator { get; }

    /// <summary>Gets the list of procedural villages generated on this island.</summary>
    public IReadOnlyList<VillageSite> Villages { get; }

    /// <summary>Initializes a new instance of the <see cref="LoadedIslandPackage"/> class.</summary>
    public LoadedIslandPackage(
        IslandLevel level,
        IReadOnlyList<ResourceSpawnPoint> resourceSpawns,
        IReadOnlyList<WaveDefinition> waves,
        BossConfiguration boss,
        int seed,
        Action<ChunkCoord, List<ResourceSpawnPoint>>? chunkResourceGenerator = null,
        Action<ChunkCoord, List<StructureSpawnPoint>>? chunkStructureGenerator = null,
        Action<ChunkCoord, List<AnimalSpawnPoint>>? chunkAnimalGenerator = null,
        IReadOnlyList<VillageSite>? villages = null)
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
        ChunkResourceGenerator = chunkResourceGenerator;
        ChunkStructureGenerator = chunkStructureGenerator;
        ChunkAnimalGenerator = chunkAnimalGenerator;
        Villages = villages ?? Array.Empty<VillageSite>();
    }
}
