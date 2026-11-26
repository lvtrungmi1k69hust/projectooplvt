using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.World;

/// <summary>
/// Manages Minecraft-style chunk streaming (loading/unloading) around the player according to
/// configurable <see cref="ViewDistanceChunks"/> while hibernating off-screen resources,
/// structures, and animals so player edits and world state persist across unload/reload cycles.
/// </summary>
public sealed class ChunkManager
{
    /// <summary>Default chunk view radius in chunks around the player (3 => 7x7 loaded chunks).</summary>
    public const int DEFAULT_VIEW_DISTANCE = 3;

    /// <summary>Minimum allowed chunk view radius.</summary>
    public const int DEFAULT_MIN_VIEW_DISTANCE = 1;

    /// <summary>Maximum allowed chunk view radius.</summary>
    public const int DEFAULT_MAX_VIEW_DISTANCE = 8;

    /// <summary>Hysteresis ring (in chunks) preventing load/unload thrashing on chunk borders.</summary>
    public const int UNLOAD_HYSTERESIS_CHUNKS = 1;

    /// <summary>Maximum non-adjacent chunks streamed in per simulation tick to prevent frame spikes.</summary>
    public const int DEFAULT_MAX_CHUNKS_PER_TICK = 12;

    private readonly IslandLevel _level;
    private readonly int _minViewDistanceChunks;
    private readonly int _maxViewDistanceChunks;
    private readonly HashSet<ChunkCoord> _generatedChunks = new();
    private readonly Dictionary<ChunkCoord, List<ResourceNode>> _hibernatedResources = new();
    private readonly Dictionary<ChunkCoord, List<Structure>> _hibernatedStructures = new();
    private readonly Dictionary<ChunkCoord, List<Animal>> _hibernatedAnimals = new();
    private readonly List<ChunkCoord> _pendingLoads = new(64);
    private readonly List<ChunkCoord> _unloadBuffer = new(64);
    private readonly List<ResourceSpawnPoint> _spawnScratch = new(32);
    private readonly List<StructureSpawnPoint> _structureSpawnScratch = new(32);
    private readonly List<AnimalSpawnPoint> _animalSpawnScratch = new(16);

    private Action<ChunkCoord, List<ResourceSpawnPoint>>? _chunkResourceGenerator;
    private Func<ResourceType, Vector2D, ResourceNode>? _resourceFactory;
    private Action<ChunkCoord, List<StructureSpawnPoint>>? _chunkStructureGenerator;
    private Func<StructureType, int, int, Structure>? _structureFactory;
    private Action<ChunkCoord, List<AnimalSpawnPoint>>? _chunkAnimalGenerator;
    private Func<AnimalType, Vector2D, Animal>? _animalFactory;

    private ChunkCoord _currentCenterChunk;
    private bool _hasInitializedCenter;
    private bool _streamingDirty = true;
    private bool _strictUnloadPending;
    private int _viewDistanceChunks;

    /// <summary>Gets the active chunk view radius around the player.</summary>
    public int ViewDistanceChunks => _viewDistanceChunks;

    /// <summary>Gets the minimum allowed view radius.</summary>
    public int MinViewDistanceChunks => _minViewDistanceChunks;

    /// <summary>Gets the maximum allowed view radius.</summary>
    public int MaxViewDistanceChunks => _maxViewDistanceChunks;

    /// <summary>Gets the chunk coordinate currently occupied by the player.</summary>
    public ChunkCoord CurrentCenterChunk => _currentCenterChunk;

    /// <summary>Gets the count of currently loaded chunks on the underlying level.</summary>
    public int LoadedChunkCount => _level.LoadedChunkCount;

    /// <summary>Gets the total chunk count of the island.</summary>
    public int TotalChunkCount => _level.TotalChunkCount;

    /// <summary>Gets the read-only set of chunks whose initial natural entities have already been generated.</summary>
    public IReadOnlyCollection<ChunkCoord> GeneratedChunks => _generatedChunks;

    /// <summary>Initializes a new instance of the <see cref="ChunkManager"/> class.</summary>
    public ChunkManager(
        IslandLevel level,
        int viewDistanceChunks = DEFAULT_VIEW_DISTANCE,
        int minViewDistanceChunks = DEFAULT_MIN_VIEW_DISTANCE,
        int maxViewDistanceChunks = DEFAULT_MAX_VIEW_DISTANCE)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (minViewDistanceChunks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minViewDistanceChunks), "Minimum view distance must be >= 1.");
        }

        if (maxViewDistanceChunks < minViewDistanceChunks)
        {
            throw new ArgumentOutOfRangeException(nameof(maxViewDistanceChunks), "Maximum view distance must be >= minimum.");
        }

        _level = level;
        _minViewDistanceChunks = minViewDistanceChunks;
        _maxViewDistanceChunks = maxViewDistanceChunks;
        _viewDistanceChunks = global::System.Math.Clamp(viewDistanceChunks, minViewDistanceChunks, maxViewDistanceChunks);
    }

    /// <summary>Configures procedural per-chunk resource generation and factory instantiation.</summary>
    public void ConfigureResourceSpawner(
        Action<ChunkCoord, List<ResourceSpawnPoint>>? generator,
        Func<ResourceType, Vector2D, ResourceNode>? factory)
    {
        ConfigureWorldSpawners(generator, factory);
    }

    /// <summary>Configures procedural per-chunk generators and factories for resources, village structures, and animals.</summary>
    public void ConfigureWorldSpawners(
        Action<ChunkCoord, List<ResourceSpawnPoint>>? resourceGenerator,
        Func<ResourceType, Vector2D, ResourceNode>? resourceFactory,
        Action<ChunkCoord, List<StructureSpawnPoint>>? structureGenerator = null,
        Func<StructureType, int, int, Structure>? structureFactory = null,
        Action<ChunkCoord, List<AnimalSpawnPoint>>? animalGenerator = null,
        Func<AnimalType, Vector2D, Animal>? animalFactory = null)
    {
        _chunkResourceGenerator = resourceGenerator;
        _resourceFactory = resourceFactory;
        _chunkStructureGenerator = structureGenerator;
        _structureFactory = structureFactory;
        _chunkAnimalGenerator = animalGenerator;
        _animalFactory = animalFactory;
    }

    /// <summary>Updates the active view distance in chunks and schedules streaming reconciliation.</summary>
    public void SetViewDistance(int viewDistanceChunks)
    {
        int clamped = global::System.Math.Clamp(viewDistanceChunks, _minViewDistanceChunks, _maxViewDistanceChunks);
        if (clamped < _viewDistanceChunks)
        {
            _strictUnloadPending = true;
        }

        _viewDistanceChunks = clamped;
        _streamingDirty = true;
    }

    /// <summary>Immediately streams in the full initial view-distance neighborhood around <paramref name="playerWorldPos"/>.</summary>
    public void InitializeAround(
        Vector2D playerWorldPos,
        Action<ResourceNode> onActivateResource,
        Action<Structure> onActivateStructure,
        Action<Animal>? onActivateAnimal = null)
    {
        ArgumentNullException.ThrowIfNull(onActivateResource);
        ArgumentNullException.ThrowIfNull(onActivateStructure);

        _streamingDirty = true;
        _hasInitializedCenter = false;
        UpdateAround(
            playerWorldPos,
            onActivateResource,
            onActivateStructure,
            _ => { },
            maxChunksPerTick: _level.TotalChunkCount,
            onActivateAnimal: onActivateAnimal);
    }

    /// <summary>
    /// Updates loaded chunks around <paramref name="playerWorldPos"/>, unloading out-of-range chunks
    /// and loading nearest pending chunks within the per-tick budget.
    /// </summary>
    public void UpdateAround(
        Vector2D playerWorldPos,
        Action<ResourceNode> onActivateResource,
        Action<Structure> onActivateStructure,
        Action<ChunkCoord> onDeactivateChunkEntities,
        int maxChunksPerTick = DEFAULT_MAX_CHUNKS_PER_TICK,
        Action<Animal>? onActivateAnimal = null)
    {
        ArgumentNullException.ThrowIfNull(onActivateResource);
        ArgumentNullException.ThrowIfNull(onActivateStructure);
        ArgumentNullException.ThrowIfNull(onDeactivateChunkEntities);

        ChunkCoord center = ClampChunkCoord(_level.GetChunkCoordForWorld(playerWorldPos));
        if (!_level.UsesLazyChunkLoading)
        {
            _currentCenterChunk = center;
            _hasInitializedCenter = true;
            _streamingDirty = false;
            return;
        }

        if (!_hasInitializedCenter || center != _currentCenterChunk || _streamingDirty)
        {
            UnloadDistantChunks(center, onDeactivateChunkEntities);
            RebuildPendingLoadQueue(center);
            _currentCenterChunk = center;
            _hasInitializedCenter = true;
            _streamingDirty = false;
        }

        ProcessPendingLoads(center, onActivateResource, onActivateStructure, onActivateAnimal, maxChunksPerTick);
    }

    /// <summary>Stores an off-screen <see cref="ResourceNode"/> until its chunk reloads.</summary>
    public void HibernateResource(ChunkCoord coord, ResourceNode resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!resource.IsAlive)
        {
            return;
        }

        if (!_hibernatedResources.TryGetValue(coord, out List<ResourceNode>? bucket))
        {
            bucket = new List<ResourceNode>(8);
            _hibernatedResources[coord] = bucket;
        }

        bucket.Add(resource);
    }

    /// <summary>Stores an off-screen <see cref="Structure"/> until its chunk reloads.</summary>
    public void HibernateStructure(ChunkCoord coord, Structure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        if (!structure.IsAlive)
        {
            return;
        }

        if (!_hibernatedStructures.TryGetValue(coord, out List<Structure>? bucket))
        {
            bucket = new List<Structure>(4);
            _hibernatedStructures[coord] = bucket;
        }

        bucket.Add(structure);
    }

    /// <summary>Stores an off-screen <see cref="Animal"/> until its chunk reloads.</summary>
    public void HibernateAnimal(ChunkCoord coord, Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        if (!animal.IsAlive)
        {
            return;
        }

        if (!_hibernatedAnimals.TryGetValue(coord, out List<Animal>? bucket))
        {
            bucket = new List<Animal>(4);
            _hibernatedAnimals[coord] = bucket;
        }

        bucket.Add(animal);
    }

    /// <summary>Enumerates all currently hibernated resources across unloaded chunks (used for snapshots).</summary>
    public IEnumerable<ResourceNode> GetHibernatedResources()
    {
        foreach (List<ResourceNode> list in _hibernatedResources.Values)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsAlive)
                {
                    yield return list[i];
                }
            }
        }
    }

    /// <summary>Enumerates all currently hibernated structures across unloaded chunks (used for snapshots).</summary>
    public IEnumerable<Structure> GetHibernatedStructures()
    {
        foreach (List<Structure> list in _hibernatedStructures.Values)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsAlive)
                {
                    yield return list[i];
                }
            }
        }
    }

    /// <summary>Enumerates all currently hibernated animals across unloaded chunks (used for snapshots).</summary>
    public IEnumerable<Animal> GetHibernatedAnimals()
    {
        foreach (List<Animal> list in _hibernatedAnimals.Values)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsAlive)
                {
                    yield return list[i];
                }
            }
        }
    }

    /// <summary>Restores previously generated chunk markers from a saved game snapshot.</summary>
    public void RestoreGeneratedChunks(IEnumerable<ChunkCoord> coords)
    {
        ArgumentNullException.ThrowIfNull(coords);
        _generatedChunks.Clear();
        foreach (ChunkCoord coord in coords)
        {
            if (_level.IsChunkInBounds(coord))
            {
                _generatedChunks.Add(coord);
            }
        }
    }

    /// <summary>Clears all hibernated entities and generated chunk markers (e.g., before loading a save).</summary>
    public void ClearHibernatedState()
    {
        _hibernatedResources.Clear();
        _hibernatedStructures.Clear();
        _hibernatedAnimals.Clear();
        _generatedChunks.Clear();
        _pendingLoads.Clear();
        _unloadBuffer.Clear();
        _hasInitializedCenter = false;
        _streamingDirty = true;
    }

    private void UnloadDistantChunks(ChunkCoord center, Action<ChunkCoord> onDeactivateChunkEntities)
    {
        int unloadRadius = _strictUnloadPending
            ? _viewDistanceChunks
            : _viewDistanceChunks + UNLOAD_HYSTERESIS_CHUNKS;

        _unloadBuffer.Clear();
        IReadOnlyList<ChunkCoord> loadedCoords = _level.LoadedChunkCoords;
        for (int i = 0; i < loadedCoords.Count; i++)
        {
            ChunkCoord loadedCoord = loadedCoords[i];
            if (loadedCoord.ChebyshevDistanceTo(center) > unloadRadius)
            {
                _unloadBuffer.Add(loadedCoord);
            }
        }

        for (int i = 0; i < _unloadBuffer.Count; i++)
        {
            ChunkCoord coord = _unloadBuffer[i];
            onDeactivateChunkEntities(coord);
            _level.UnloadChunk(coord);
        }

        _strictUnloadPending = false;
    }

    private void RebuildPendingLoadQueue(ChunkCoord center)
    {
        _pendingLoads.Clear();

        int minCx = global::System.Math.Max(0, center.ChunkX - _viewDistanceChunks);
        int maxCx = global::System.Math.Min(_level.ChunkColumns - 1, center.ChunkX + _viewDistanceChunks);
        int minCy = global::System.Math.Max(0, center.ChunkY - _viewDistanceChunks);
        int maxCy = global::System.Math.Min(_level.ChunkRows - 1, center.ChunkY + _viewDistanceChunks);

        for (int cy = minCy; cy <= maxCy; cy++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                var coord = new ChunkCoord(cx, cy);
                if (!_level.IsChunkLoaded(coord) || !_generatedChunks.Contains(coord))
                {
                    _pendingLoads.Add(coord);
                }
            }
        }

        _pendingLoads.Sort((a, b) => a.SquaredDistanceTo(center).CompareTo(b.SquaredDistanceTo(center)));
    }

    private void ProcessPendingLoads(
        ChunkCoord center,
        Action<ResourceNode> onActivateResource,
        Action<Structure> onActivateStructure,
        Action<Animal>? onActivateAnimal,
        int maxChunksPerTick)
    {
        if (_pendingLoads.Count == 0)
        {
            return;
        }

        // Guarantee immediate 3x3 ring around player is always loaded without delay.
        for (int i = _pendingLoads.Count - 1; i >= 0; i--)
        {
            ChunkCoord coord = _pendingLoads[i];
            if (coord.ChebyshevDistanceTo(center) <= 1)
            {
                _pendingLoads.RemoveAt(i);
                LoadAndActivateSingleChunk(coord, onActivateResource, onActivateStructure, onActivateAnimal);
            }
        }

        int budget = global::System.Math.Max(0, maxChunksPerTick);
        int loadedThisTick = 0;
        while (_pendingLoads.Count > 0 && loadedThisTick < budget)
        {
            ChunkCoord coord = _pendingLoads[0];
            _pendingLoads.RemoveAt(0);

            if (coord.ChebyshevDistanceTo(center) > _viewDistanceChunks ||
                (_level.IsChunkLoaded(coord) && _generatedChunks.Contains(coord)))
            {
                continue;
            }

            LoadAndActivateSingleChunk(coord, onActivateResource, onActivateStructure, onActivateAnimal);
            loadedThisTick++;
        }
    }

    private void LoadAndActivateSingleChunk(
        ChunkCoord coord,
        Action<ResourceNode> onActivateResource,
        Action<Structure> onActivateStructure,
        Action<Animal>? onActivateAnimal)
    {
        if (!_level.IsChunkInBounds(coord))
        {
            return;
        }

        _level.LoadChunk(coord);
        bool isFirstGeneration = _generatedChunks.Add(coord);

        // 1. Structures first so solid occupancy is registered before resource/animal spawning.
        if (_hibernatedStructures.Remove(coord, out List<Structure>? savedStructures))
        {
            for (int i = 0; i < savedStructures.Count; i++)
            {
                if (savedStructures[i].IsAlive)
                {
                    onActivateStructure(savedStructures[i]);
                }
            }
        }
        else if (isFirstGeneration && _chunkStructureGenerator is not null && _structureFactory is not null)
        {
            _structureSpawnScratch.Clear();
            _chunkStructureGenerator(coord, _structureSpawnScratch);
            for (int i = 0; i < _structureSpawnScratch.Count; i++)
            {
                StructureSpawnPoint sp = _structureSpawnScratch[i];
                onActivateStructure(_structureFactory(sp.StructureType, sp.TileCol, sp.TileRow));
            }
        }

        // 2. Natural resources & village crops.
        if (_hibernatedResources.Remove(coord, out List<ResourceNode>? savedResources))
        {
            for (int i = 0; i < savedResources.Count; i++)
            {
                if (savedResources[i].IsAlive)
                {
                    onActivateResource(savedResources[i]);
                }
            }
        }
        else if (isFirstGeneration && _chunkResourceGenerator is not null && _resourceFactory is not null)
        {
            _spawnScratch.Clear();
            _chunkResourceGenerator(coord, _spawnScratch);
            for (int i = 0; i < _spawnScratch.Count; i++)
            {
                ResourceSpawnPoint pt = _spawnScratch[i];
                Vector2D worldPos = new(pt.TileCol * _level.TileSize, pt.TileRow * _level.TileSize);
                onActivateResource(_resourceFactory(pt.ResourceType, worldPos));
            }
        }

        // 3. Village livestock & wild animals.
        if (_hibernatedAnimals.Remove(coord, out List<Animal>? savedAnimals))
        {
            if (onActivateAnimal is not null)
            {
                for (int i = 0; i < savedAnimals.Count; i++)
                {
                    if (savedAnimals[i].IsAlive)
                    {
                        onActivateAnimal(savedAnimals[i]);
                    }
                }
            }
        }
        else if (isFirstGeneration && _chunkAnimalGenerator is not null && _animalFactory is not null && onActivateAnimal is not null)
        {
            _animalSpawnScratch.Clear();
            _chunkAnimalGenerator(coord, _animalSpawnScratch);
            for (int i = 0; i < _animalSpawnScratch.Count; i++)
            {
                AnimalSpawnPoint ap = _animalSpawnScratch[i];
                Vector2D worldPos = new((ap.TileCol * _level.TileSize) + 4f, (ap.TileRow * _level.TileSize) + 4f);
                onActivateAnimal(_animalFactory(ap.AnimalType, worldPos));
            }
        }
    }

    private ChunkCoord ClampChunkCoord(ChunkCoord raw)
    {
        return new ChunkCoord(
            global::System.Math.Clamp(raw.ChunkX, 0, _level.ChunkColumns - 1),
            global::System.Math.Clamp(raw.ChunkY, 0, _level.ChunkRows - 1));
    }
}
