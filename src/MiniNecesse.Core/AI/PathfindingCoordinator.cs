using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;

namespace MiniNecesse.Core.AI;

/// <summary>
/// Shared AI pathfinding coordinator providing bounded per-tick repath budgeting,
/// capped A* node expansions, and zero-allocation path caching across enemies.
/// </summary>
public sealed class PathfindingCoordinator
{
    /// <summary>Default per-search A* expansion cap keeping worst-case queries deterministic.</summary>
    public const int DEFAULT_MAX_NODE_ITERATIONS = 250;

    /// <summary>Default maximum fresh A* searches executed during one simulation tick.</summary>
    public const int DEFAULT_MAX_REPATHS_PER_TICK = 3;

    /// <summary>Default cached path lifetime in seconds before periodic refresh.</summary>
    public const float DEFAULT_CACHE_TTL_SECONDS = 0.75f;

    /// <summary>Default number of reusable path slots retained in the ring cache.</summary>
    public const int DEFAULT_CACHE_CAPACITY = 64;

    private readonly record struct PathCacheKey(GridPoint Start, GridPoint Goal) : IEquatable<PathCacheKey>;

    private sealed class CacheSlot
    {
        public bool IsValid;
        public GridPoint Start;
        public GridPoint Goal;
        public int StructureVersion;
        public float CachedAtSeconds;
        public PathResult Result;
        public readonly List<GridPoint> Waypoints = new(64);
    }

    private readonly AStarPathfinder _pathfinder = new();
    private readonly int _maxNodeIterations;
    private readonly int _maxRepathsPerTick;
    private readonly float _cacheTtlSeconds;
    private readonly CacheSlot[] _cacheSlots;
    private readonly Dictionary<PathCacheKey, int> _slotByKey;

    private float _clockSeconds;
    private int _repathsUsedThisTick;
    private int _nextSlotIndex;

    /// <summary>Gets the configured per-search A* iteration budget.</summary>
    public int MaxNodeIterations => _maxNodeIterations;

    /// <summary>Gets the maximum number of uncached A* searches allowed per tick.</summary>
    public int MaxRepathsPerTick => _maxRepathsPerTick;

    /// <summary>Gets the cache entry time-to-live in seconds.</summary>
    public float CacheTtlSeconds => _cacheTtlSeconds;

    /// <summary>Gets total path requests served directly from cache.</summary>
    public int CacheHits { get; private set; }

    /// <summary>Gets total uncached A* path computations executed.</summary>
    public int CacheMisses { get; private set; }

    /// <summary>Gets total path requests deferred to a later tick due to budget throttling.</summary>
    public int DeferredRequests { get; private set; }

    /// <summary>Gets total A* searches actually executed through the underlying pathfinder.</summary>
    public int TotalSearches => CacheMisses;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathfindingCoordinator"/> class.
    /// </summary>
    public PathfindingCoordinator(
        int maxNodeIterations = DEFAULT_MAX_NODE_ITERATIONS,
        int maxRepathsPerTick = DEFAULT_MAX_REPATHS_PER_TICK,
        float cacheTtlSeconds = DEFAULT_CACHE_TTL_SECONDS,
        int cacheCapacity = DEFAULT_CACHE_CAPACITY)
    {
        if (maxNodeIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNodeIterations), "Max node iterations must be positive.");
        }

        if (maxRepathsPerTick <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRepathsPerTick), "Max repaths per tick must be positive.");
        }

        if (cacheTtlSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheTtlSeconds), "Cache TTL must be positive.");
        }

        if (cacheCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cacheCapacity), "Cache capacity must be positive.");
        }

        _maxNodeIterations = maxNodeIterations;
        _maxRepathsPerTick = maxRepathsPerTick;
        _cacheTtlSeconds = cacheTtlSeconds;
        _cacheSlots = new CacheSlot[cacheCapacity];
        for (int i = 0; i < cacheCapacity; i++)
        {
            _cacheSlots[i] = new CacheSlot();
        }

        _slotByKey = new Dictionary<PathCacheKey, int>(cacheCapacity);
    }

    /// <summary>Advances the coordinator clock and resets the per-tick repath budget.</summary>
    public void BeginTick(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        _clockSeconds += deltaTime;
        _repathsUsedThisTick = 0;
    }

    /// <summary>Invalidates all cached paths and resets per-tick budget counters.</summary>
    public void ClearCache()
    {
        for (int i = 0; i < _cacheSlots.Length; i++)
        {
            _cacheSlots[i].IsValid = false;
            _cacheSlots[i].Waypoints.Clear();
        }

        _slotByKey.Clear();
        _repathsUsedThisTick = 0;
        _nextSlotIndex = 0;
    }

    /// <summary>
    /// Resolves a path from cache or computes a bounded A* path if tick budget remains.
    /// Returns <see langword="false"/> (leaving <paramref name="outputBuffer"/> intact) when deferred due to per-tick budget.
    /// </summary>
    public bool TryRequestPath(
        IslandLevel level,
        GridPoint start,
        GridPoint goal,
        List<GridPoint> outputBuffer,
        out PathResult result,
        out bool servedFromCache)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(outputBuffer);

        var key = new PathCacheKey(start, goal);
        if (_slotByKey.TryGetValue(key, out int cachedSlotIndex))
        {
            CacheSlot cached = _cacheSlots[cachedSlotIndex];
            if (cached.IsValid &&
                cached.StructureVersion == level.StructureVersion &&
                (_clockSeconds - cached.CachedAtSeconds) <= _cacheTtlSeconds)
            {
                CopyWaypoints(cached.Waypoints, outputBuffer);
                result = cached.Result;
                servedFromCache = true;
                CacheHits++;
                return true;
            }
        }

        if (_repathsUsedThisTick >= _maxRepathsPerTick)
        {
            DeferredRequests++;
            result = default;
            servedFromCache = false;
            return false;
        }

        _repathsUsedThisTick++;
        CacheMisses++;
        result = _pathfinder.FindPath(level, start, goal, outputBuffer, _maxNodeIterations);
        servedFromCache = false;

        if (outputBuffer.Count > 0 && start != goal)
        {
            StoreSlot(key, level.StructureVersion, result, outputBuffer);
        }

        return true;
    }

    private void StoreSlot(PathCacheKey key, int structureVersion, PathResult result, List<GridPoint> waypoints)
    {
        if (_slotByKey.TryGetValue(key, out int existingIndex))
        {
            PopulateSlot(_cacheSlots[existingIndex], key, structureVersion, result, waypoints);
            return;
        }

        int slotIndex = _nextSlotIndex;
        _nextSlotIndex = (_nextSlotIndex + 1) % _cacheSlots.Length;

        CacheSlot slot = _cacheSlots[slotIndex];
        if (slot.IsValid)
        {
            _slotByKey.Remove(new PathCacheKey(slot.Start, slot.Goal));
        }

        PopulateSlot(slot, key, structureVersion, result, waypoints);
        _slotByKey[key] = slotIndex;
    }

    private void PopulateSlot(CacheSlot slot, PathCacheKey key, int structureVersion, PathResult result, List<GridPoint> waypoints)
    {
        slot.IsValid = true;
        slot.Start = key.Start;
        slot.Goal = key.Goal;
        slot.StructureVersion = structureVersion;
        slot.CachedAtSeconds = _clockSeconds;
        slot.Result = result;
        CopyWaypoints(waypoints, slot.Waypoints);
    }

    private static void CopyWaypoints(List<GridPoint> source, List<GridPoint> destination)
    {
        destination.Clear();
        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }
    }
}
