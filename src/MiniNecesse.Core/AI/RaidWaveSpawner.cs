using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;

namespace MiniNecesse.Core.AI;

/// <summary>
/// Immutable spawn entry specifying how many enemies of a given archetype spawn in a raid wave.
/// </summary>
public sealed class WaveSpawnEntry
{
    /// <summary>Gets the enemy archetype identifier (e.g., "Slime", "ZombieRaider").</summary>
    public string EnemyArchetype { get; }

    /// <summary>Gets the base count of enemies before difficulty scaling.</summary>
    public int Count { get; }

    /// <summary>Initializes a new instance of the <see cref="WaveSpawnEntry"/> class.</summary>
    public WaveSpawnEntry(string enemyArchetype, int count)
    {
        if (string.IsNullOrWhiteSpace(enemyArchetype))
        {
            throw new ArgumentException("Enemy archetype cannot be empty.", nameof(enemyArchetype));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Spawn count must be positive.");
        }

        EnemyArchetype = enemyArchetype;
        Count = count;
    }
}

/// <summary>
/// Immutable definition of one island raid wave loaded from JSON level data.
/// </summary>
public sealed class WaveDefinition
{
    private readonly List<WaveSpawnEntry> _entries;
    private readonly IReadOnlyList<WaveSpawnEntry> _entriesView;

    /// <summary>Gets the 1-based wave number.</summary>
    public int WaveNumber { get; }

    /// <summary>Gets the cached read-only spawn entries composing this wave.</summary>
    public IReadOnlyList<WaveSpawnEntry> Entries => _entriesView;

    /// <summary>Initializes a new instance of the <see cref="WaveDefinition"/> class.</summary>
    public WaveDefinition(int waveNumber, IEnumerable<WaveSpawnEntry> entries)
    {
        if (waveNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waveNumber), "Wave number must be positive.");
        }

        ArgumentNullException.ThrowIfNull(entries);
        _entries = new List<WaveSpawnEntry>(entries);
        if (_entries.Count == 0)
        {
            throw new ArgumentException("Wave must contain at least one spawn entry.", nameof(entries));
        }

        _entriesView = _entries.AsReadOnly();
        WaveNumber = waveNumber;
    }
}

/// <summary>
/// Orchestrates nighttime island raid waves scaled by <see cref="Difficulty"/> with safe tile validation.
/// <para>
/// Pseudocode:
/// 1. Listen for nighttime phase (or explicit wave trigger) while waves remain.
/// 2. Scale each entry's count by difficulty multiplier (Easy: 0.75x, Normal: 1.0x, Hard: 1.5x).
/// 3. For each enemy to spawn:
///    a. Sample candidate tiles on IslandLevel up to MAX_SPAWN_ATTEMPTS.
///    b. Verify tile is walkable (not Water, not solid Structure) and outside minSafeRadius from Player.
///    c. Fallback to deterministic grid scan if random sampling misses; skip safely if no tile qualifies.
/// 4. Invoke injected enemy creator delegate and emit OnWaveSpawned / OnAllWavesSpawned events.
/// </para>
/// <para>
/// Complexity: O(M * K) per wave where M is total enemies in wave and K &lt;= MAX_SPAWN_ATTEMPTS (fallback O(W*H)).
/// </para>
/// <para>
/// Edge cases: Never spawns enemies on water, inside solid walls, outside map bounds, or on top of the player.
/// </para>
/// </summary>
public sealed class RaidWaveSpawner
{
    private const int MAX_SPAWN_ATTEMPTS = 40;
    private const float DEFAULT_MIN_SAFE_RADIUS = 160f;

    private static readonly Dictionary<Difficulty, float> DIFFICULTY_COUNT_MULTIPLIERS = new()
    {
        [Difficulty.Easy] = 0.75f,
        [Difficulty.Normal] = 1.0f,
        [Difficulty.Hard] = 1.5f
    };

    private readonly IslandLevel _level;
    private readonly Difficulty _difficulty;
    private readonly float _raidIntensityMultiplier;
    private readonly List<WaveDefinition> _waves;
    private readonly Func<string, Vector2D, Difficulty, Enemy> _enemyCreator;
    private readonly float _minSafeRadius;
    private readonly Random _random;
    private int _nextWaveIndex;

    /// <summary>Gets the 0-based index of the next wave waiting to spawn.</summary>
    public int NextWaveIndex => _nextWaveIndex;

    /// <summary>Gets total configured raid waves for this island.</summary>
    public int TotalWaves => _waves.Count;

    /// <summary>Gets a value indicating whether all configured waves have been spawned.</summary>
    public bool HasCompletedAllWaves => _nextWaveIndex >= _waves.Count;

    /// <summary>Raised after a raid wave finishes spawning, reporting (waveNumber, spawnedCount).</summary>
    public event Action<int, int>? OnWaveSpawned;

    /// <summary>Raised once all configured island waves have been dispatched (signaling boss readiness).</summary>
    public event Action? OnAllWavesSpawned;

    /// <summary>
    /// Initializes a new instance of the <see cref="RaidWaveSpawner"/> class.
    /// </summary>
    public RaidWaveSpawner(
        IslandLevel level,
        Difficulty difficulty,
        IEnumerable<WaveDefinition> waves,
        Func<string, Vector2D, Difficulty, Enemy> enemyCreator,
        float minSafeRadius = DEFAULT_MIN_SAFE_RADIUS,
        int? seed = null,
        DifficultyProfile? difficultyProfile = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(waves);
        ArgumentNullException.ThrowIfNull(enemyCreator);

        if (minSafeRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(minSafeRadius), "Safe spawn radius cannot be negative.");
        }

        _level = level;
        _difficulty = difficulty;
        _raidIntensityMultiplier = difficultyProfile is { RaidIntensity: > 0f }
            ? difficultyProfile.RaidIntensity
            : (DIFFICULTY_COUNT_MULTIPLIERS.TryGetValue(difficulty, out float m) ? m : 1.0f);
        _waves = new List<WaveDefinition>(waves);
        _enemyCreator = enemyCreator;
        _minSafeRadius = minSafeRadius;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Restores the wave index when loading a saved game snapshot.</summary>
    public void RestoreWaveIndex(int nextWaveIndex)
    {
        if (nextWaveIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextWaveIndex), "Wave index cannot be negative.");
        }

        _nextWaveIndex = global::System.Math.Min(nextWaveIndex, _waves.Count);
    }

    /// <summary>
    /// Spawns the next scheduled raid wave into <paramref name="spawnedOutput"/> (reusable caller buffer)
    /// while respecting safe distance around <paramref name="player"/> and walkable terrain constraints.
    /// </summary>
    public int SpawnNextWave(Player player, List<Enemy> spawnedOutput)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(spawnedOutput);
        spawnedOutput.Clear();

        if (HasCompletedAllWaves)
        {
            return 0;
        }

        WaveDefinition wave = _waves[_nextWaveIndex];
        float multiplier = _raidIntensityMultiplier;

        for (int i = 0; i < wave.Entries.Count; i++)
        {
            WaveSpawnEntry entry = wave.Entries[i];
            int scaledCount = global::System.Math.Max(1, (int)MathF.Round(entry.Count * multiplier));
            SpawnArchetypeGroup(entry.EnemyArchetype, scaledCount, player.Center, spawnedOutput);
        }

        _nextWaveIndex++;
        OnWaveSpawned?.Invoke(wave.WaveNumber, spawnedOutput.Count);

        if (HasCompletedAllWaves)
        {
            OnAllWavesSpawned?.Invoke();
        }

        return spawnedOutput.Count;
    }

    /// <summary>
    /// Spawns a continuous nighttime skirmish reinforcement wave by cycling the island's configured waves
    /// so players can keep farming enemies every night indefinitely.
    /// </summary>
    public int SpawnNightSkirmishWave(Player player, List<Enemy> spawnedOutput)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(spawnedOutput);
        spawnedOutput.Clear();

        if (_waves.Count == 0)
        {
            return 0;
        }

        int waveSlot = _nextWaveIndex < _waves.Count ? _nextWaveIndex : _waves.Count - 1;
        WaveDefinition wave = _waves[waveSlot];
        float multiplier = DIFFICULTY_COUNT_MULTIPLIERS.TryGetValue(_difficulty, out float m) ? m : 1.0f;

        for (int i = 0; i < wave.Entries.Count; i++)
        {
            WaveSpawnEntry entry = wave.Entries[i];
            int scaledCount = global::System.Math.Max(1, (int)MathF.Round(entry.Count * multiplier));
            SpawnArchetypeGroup(entry.EnemyArchetype, scaledCount, player.Center, spawnedOutput);
        }

        _nextWaveIndex++;
        OnWaveSpawned?.Invoke(_nextWaveIndex, spawnedOutput.Count);
        return spawnedOutput.Count;
    }

    /// <summary>
    /// Finds a valid walkable world position outside the player's safe exclusion radius
    /// and within loaded chunk bounds near the player on large worlds.
    /// Returns <see langword="false"/> if no walkable tile exists outside the exclusion circle.
    /// </summary>
    public bool TryFindSafeSpawnPosition(Vector2D playerCenter, out Vector2D spawnPosition)
    {
        float minSafeRadiusSq = _minSafeRadius * _minSafeRadius;
        bool useLocalRadius = _level.UsesLazyChunkLoading || _level.Width > 64 || _level.Height > 64;

        if (useLocalRadius)
        {
            (int pCol, int pRow) = _level.WorldToTile(playerCenter);
            float maxSpawnRadius = MathF.Max(_minSafeRadius + 128f, 560f);
            float maxSpawnRadiusSq = maxSpawnRadius * maxSpawnRadius;
            int maxTileOffset = global::System.Math.Max(4, (int)MathF.Ceiling(maxSpawnRadius / _level.TileSize));

            int minCol = global::System.Math.Max(0, pCol - maxTileOffset);
            int maxCol = global::System.Math.Min(_level.Width - 1, pCol + maxTileOffset);
            int minRow = global::System.Math.Max(0, pRow - maxTileOffset);
            int maxRow = global::System.Math.Min(_level.Height - 1, pRow + maxTileOffset);

            for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
            {
                int col = _random.Next(minCol, maxCol + 1);
                int row = _random.Next(minRow, maxRow + 1);
                if (IsValidSpawnTile(col, row, playerCenter, minSafeRadiusSq, out spawnPosition) &&
                    Vector2D.DistanceSquared(spawnPosition, playerCenter) <= maxSpawnRadiusSq)
                {
                    return true;
                }
            }

            return TryLoadedChunkFallbackScan(playerCenter, minSafeRadiusSq, out spawnPosition);
        }

        for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            int col = _random.Next(0, _level.Width);
            int row = _random.Next(0, _level.Height);
            if (IsValidSpawnTile(col, row, playerCenter, minSafeRadiusSq, out spawnPosition))
            {
                return true;
            }
        }

        return TryDeterministicFallbackScan(playerCenter, minSafeRadiusSq, out spawnPosition);
    }

    private void SpawnArchetypeGroup(string archetype, int count, Vector2D playerCenter, List<Enemy> output)
    {
        for (int i = 0; i < count; i++)
        {
            if (TryFindSafeSpawnPosition(playerCenter, out Vector2D spawnPos))
            {
                output.Add(_enemyCreator(archetype, spawnPos, _difficulty));
            }
        }
    }

    private bool IsValidSpawnTile(int col, int row, Vector2D playerCenter, float minSafeRadiusSq, out Vector2D worldPos)
    {
        worldPos = Vector2D.Zero;
        if (!_level.IsTileWalkable(col, row))
        {
            return false;
        }

        Vector2D candidate = _level.TileToWorldCenter(col, row);
        if (Vector2D.DistanceSquared(candidate, playerCenter) < minSafeRadiusSq)
        {
            return false;
        }

        worldPos = candidate;
        return true;
    }

    private bool TryLoadedChunkFallbackScan(Vector2D playerCenter, float minSafeRadiusSq, out Vector2D spawnPosition)
    {
        IReadOnlyList<ChunkCoord> loadedCoords = _level.LoadedChunkCoords;
        for (int i = 0; i < loadedCoords.Count; i++)
        {
            if (!_level.TryGetLoadedChunk(loadedCoords[i], out WorldChunk? chunk) || chunk is null)
            {
                continue;
            }

            for (int r = 0; r < chunk.Height; r++)
            {
                for (int c = 0; c < chunk.Width; c++)
                {
                    Tile tile = chunk.GetLocalTile(c, r);
                    if (IsValidSpawnTile(tile.Col, tile.Row, playerCenter, minSafeRadiusSq, out spawnPosition))
                    {
                        return true;
                    }
                }
            }
        }

        spawnPosition = Vector2D.Zero;
        return false;
    }

    private bool TryDeterministicFallbackScan(Vector2D playerCenter, float minSafeRadiusSq, out Vector2D spawnPosition)
    {
        for (int row = 0; row < _level.Height; row++)
        {
            for (int col = 0; col < _level.Width; col++)
            {
                if (IsValidSpawnTile(col, row, playerCenter, minSafeRadiusSq, out spawnPosition))
                {
                    return true;
                }
            }
        }

        spawnPosition = Vector2D.Zero;
        return false;
    }
}
