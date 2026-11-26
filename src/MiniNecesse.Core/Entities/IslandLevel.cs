using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.World;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a 2D top-down island tilemap partitioned into streamable <see cref="WorldChunk"/> regions.
/// Demonstrates UML Composition by internally creating and exclusively owning its chunked <see cref="Tile"/> instances.
/// </summary>
public sealed class IslandLevel
{
    /// <summary>Default tile width and height of each square world chunk (16x16 tiles, Minecraft-style).</summary>
    public const int DEFAULT_CHUNK_SIZE = 16;

    private readonly string _islandId;
    private readonly string _name;
    private readonly string _biome;
    private readonly string _bossType;
    private readonly int _width;
    private readonly int _height;
    private readonly float _tileSize;
    private readonly int _spawnCol;
    private readonly int _spawnRow;
    private readonly int _chunkSize;
    private readonly int _chunkColumns;
    private readonly int _chunkRows;
    private readonly bool _lazyChunkLoading;
    private readonly Func<int, int, TileType> _terrainGenerator;

    private readonly Dictionary<ChunkCoord, WorldChunk> _loadedChunks = new();
    private readonly List<ChunkCoord> _loadedChunkCoordsList = new();
    private readonly IReadOnlyList<ChunkCoord> _loadedChunkCoordsView;
    private readonly Dictionary<GridPoint, TileType> _tileTypeOverrides = new();
    private readonly HashSet<GridPoint> _occupiedStructureTiles = new();
    private readonly List<Tile> _loadedTilesCache = new();
    private readonly IReadOnlyList<Tile> _loadedTilesView;
    private bool _loadedTilesDirty = true;
    private int _structureVersion;

    /// <summary>Gets the unique island identifier (e.g., ISLAND_01).</summary>
    public string IslandId => _islandId;

    /// <summary>Gets the display name of the island.</summary>
    public string Name => _name;

    /// <summary>Gets the biome name of the island.</summary>
    public string Biome => _biome;

    /// <summary>Gets the archetype identifier of this island's boss.</summary>
    public string BossType => _bossType;

    /// <summary>Gets the width of the island grid in tiles.</summary>
    public int Width => _width;

    /// <summary>Gets the height of the island grid in tiles.</summary>
    public int Height => _height;

    /// <summary>Gets the pixel/world size of each square tile.</summary>
    public float TileSize => _tileSize;

    /// <summary>Gets the width and height of one world chunk in tiles.</summary>
    public int ChunkSize => _chunkSize;

    /// <summary>Gets total horizontal chunk columns across the island.</summary>
    public int ChunkColumns => _chunkColumns;

    /// <summary>Gets total vertical chunk rows across the island.</summary>
    public int ChunkRows => _chunkRows;

    /// <summary>Gets total number of chunks covering this island.</summary>
    public int TotalChunkCount => _chunkColumns * _chunkRows;

    /// <summary>Gets the number of chunks currently loaded in memory.</summary>
    public int LoadedChunkCount => _loadedChunks.Count;

    /// <summary>Gets a value indicating whether chunks are streamed lazily around the player.</summary>
    public bool UsesLazyChunkLoading => _lazyChunkLoading;

    /// <summary>Gets the cached read-only list of coordinates of all currently loaded chunks.</summary>
    public IReadOnlyList<ChunkCoord> LoadedChunkCoords => _loadedChunkCoordsView;

    /// <summary>Gets the monotonic terrain/structure walkability version for path cache invalidation.</summary>
    public int StructureVersion => _structureVersion;

    /// <summary>Gets the total world width in world units.</summary>
    public float WorldWidth => _width * _tileSize;

    /// <summary>Gets the total world height in world units.</summary>
    public float WorldHeight => _height * _tileSize;

    /// <summary>Gets the player spawn tile column.</summary>
    public int SpawnCol => _spawnCol;

    /// <summary>Gets the player spawn tile row.</summary>
    public int SpawnRow => _spawnRow;

    /// <summary>Gets the world-space center position of the player spawn tile.</summary>
    public Vector2D PlayerSpawnWorldPosition => TileToWorldCenter(_spawnCol, _spawnRow);

    /// <summary>Gets a cached read-only view of all currently loaded tiles owned by this island.</summary>
    public IReadOnlyList<Tile> Tiles
    {
        get
        {
            if (_loadedTilesDirty)
            {
                RebuildLoadedTilesCache();
            }

            return _loadedTilesView;
        }
    }

    /// <summary>Raised whenever a chunk is streamed into memory.</summary>
    public event Action<WorldChunk>? OnChunkLoaded;

    /// <summary>Raised whenever a chunk is unloaded from memory.</summary>
    public event Action<ChunkCoord>? OnChunkUnloaded;

    /// <summary>
    /// Initializes a new <see cref="IslandLevel"/> and prepares its chunked <see cref="Tile"/> storage.
    /// </summary>
    /// <exception cref="InvalidLevelDataException">Thrown when dimensions, identifiers, chunk size, or spawn coordinates are invalid.</exception>
    public IslandLevel(
        string islandId,
        string name,
        string biome,
        string bossType,
        int width,
        int height,
        int spawnCol,
        int spawnRow,
        float tileSize = 32f,
        Func<int, int, TileType>? terrainGenerator = null,
        int chunkSize = DEFAULT_CHUNK_SIZE,
        bool lazyChunkLoading = false)
    {
        ValidateLevelMetadata(islandId, name, bossType, width, height, spawnCol, spawnRow, tileSize, chunkSize);

        _islandId = islandId;
        _name = name;
        _biome = string.IsNullOrWhiteSpace(biome) ? "Forest" : biome;
        _bossType = bossType;
        _width = width;
        _height = height;
        _spawnCol = spawnCol;
        _spawnRow = spawnRow;
        _tileSize = tileSize;
        _chunkSize = chunkSize;
        _chunkColumns = (width + chunkSize - 1) / chunkSize;
        _chunkRows = (height + chunkSize - 1) / chunkSize;
        _lazyChunkLoading = lazyChunkLoading;
        _terrainGenerator = terrainGenerator ?? DefaultIslandTerrain;
        _loadedChunkCoordsView = _loadedChunkCoordsList.AsReadOnly();
        _loadedTilesView = _loadedTilesCache.AsReadOnly();

        if (_lazyChunkLoading)
        {
            LoadChunk(GetChunkCoordForTile(spawnCol, spawnRow));
        }
        else
        {
            for (int cy = 0; cy < _chunkRows; cy++)
            {
                for (int cx = 0; cx < _chunkColumns; cx++)
                {
                    LoadChunk(new ChunkCoord(cx, cy));
                }
            }
        }

        EnsureSpawnTileIsWalkable();
    }

    /// <summary>Checks whether (<paramref name="col"/>, <paramref name="row"/>) lies inside the island grid.</summary>
    public bool IsInBounds(int col, int row) => col >= 0 && col < _width && row >= 0 && row < _height;

    /// <summary>Converts a tile grid coordinate to its owning <see cref="ChunkCoord"/>.</summary>
    public ChunkCoord GetChunkCoordForTile(int col, int row) => ChunkCoord.FromTile(col, row, _chunkSize);

    /// <summary>Converts a world-space coordinate to its owning <see cref="ChunkCoord"/>.</summary>
    public ChunkCoord GetChunkCoordForWorld(Vector2D worldPosition) => ChunkCoord.FromWorld(worldPosition, _tileSize, _chunkSize);

    /// <summary>Checks whether <paramref name="coord"/> is within the island chunk grid.</summary>
    public bool IsChunkInBounds(ChunkCoord coord)
    {
        return coord.ChunkX >= 0 && coord.ChunkX < _chunkColumns && coord.ChunkY >= 0 && coord.ChunkY < _chunkRows;
    }

    /// <summary>Returns <see langword="true"/> if <paramref name="coord"/> is currently loaded in memory.</summary>
    public bool IsChunkLoaded(ChunkCoord coord) => _loadedChunks.ContainsKey(coord);

    /// <summary>Attempts to retrieve an already loaded chunk without triggering loading.</summary>
    public bool TryGetLoadedChunk(ChunkCoord coord, out WorldChunk? chunk) => _loadedChunks.TryGetValue(coord, out chunk);

    /// <summary>Loads <paramref name="coord"/> into memory if not already loaded and returns it.</summary>
    public WorldChunk LoadChunk(ChunkCoord coord)
    {
        if (!IsChunkInBounds(coord))
        {
            throw new ArgumentOutOfRangeException(nameof(coord), $"Chunk {coord} is outside {_chunkColumns}x{_chunkRows}.");
        }

        if (_loadedChunks.TryGetValue(coord, out WorldChunk? existing))
        {
            return existing;
        }

        int startCol = coord.ChunkX * _chunkSize;
        int startRow = coord.ChunkY * _chunkSize;
        int chunkWidth = global::System.Math.Min(_chunkSize, _width - startCol);
        int chunkHeight = global::System.Math.Min(_chunkSize, _height - startRow);

        var tiles = new Tile[chunkWidth * chunkHeight];
        for (int localRow = 0; localRow < chunkHeight; localRow++)
        {
            int globalRow = startRow + localRow;
            for (int localCol = 0; localCol < chunkWidth; localCol++)
            {
                int globalCol = startCol + localCol;
                var point = new GridPoint(globalCol, globalRow);
                TileType tileType = _tileTypeOverrides.TryGetValue(point, out TileType overridden)
                    ? overridden
                    : _terrainGenerator(globalCol, globalRow);

                var tile = new Tile(globalCol, globalRow, tileType);
                if (_occupiedStructureTiles.Contains(point))
                {
                    tile.SetSolidStructureOccupied(true);
                }

                tiles[(localRow * chunkWidth) + localCol] = tile;
            }
        }

        var chunk = new WorldChunk(coord, startCol, startRow, chunkWidth, chunkHeight, tiles);
        _loadedChunks[coord] = chunk;
        _loadedChunkCoordsList.Add(coord);
        _loadedTilesDirty = true;
        _structureVersion++;
        OnChunkLoaded?.Invoke(chunk);
        return chunk;
    }

    /// <summary>Unloads <paramref name="coord"/> from memory, releasing its tile array while preserving overrides.</summary>
    public bool UnloadChunk(ChunkCoord coord)
    {
        if (_loadedChunks.Remove(coord, out WorldChunk? removed))
        {
            _loadedChunkCoordsList.Remove(coord);
            removed.MarkUnloaded();
            _loadedTilesDirty = true;
            _structureVersion++;
            OnChunkUnloaded?.Invoke(coord);
            return true;
        }

        return false;
    }

    /// <summary>Attempts to retrieve a tile only if its owning chunk is currently loaded.</summary>
    public bool TryGetLoadedTile(int col, int row, out Tile? tile)
    {
        if (!IsInBounds(col, row))
        {
            tile = null;
            return false;
        }

        ChunkCoord coord = GetChunkCoordForTile(col, row);
        if (!_loadedChunks.TryGetValue(coord, out WorldChunk? chunk))
        {
            tile = null;
            return false;
        }

        tile = chunk.GetLocalTile(col - chunk.StartCol, row - chunk.StartRow);
        return true;
    }

    /// <summary>Returns the tile at (<paramref name="col"/>, <paramref name="row"/>), loading its chunk on demand if necessary.</summary>
    public Tile GetTile(int col, int row)
    {
        if (!IsInBounds(col, row))
        {
            throw new ArgumentOutOfRangeException(nameof(col), $"Tile ({col}, {row}) is outside {_width}x{_height}.");
        }

        ChunkCoord coord = GetChunkCoordForTile(col, row);
        WorldChunk chunk = _loadedChunks.TryGetValue(coord, out WorldChunk? loaded) ? loaded : LoadChunk(coord);
        return chunk.GetLocalTile(col - chunk.StartCol, row - chunk.StartRow);
    }

    /// <summary>Attempts to retrieve the loaded tile at (<paramref name="col"/>, <paramref name="row"/>) without throwing or force-loading.</summary>
    public bool TryGetTile(int col, int row, out Tile? tile) => TryGetLoadedTile(col, row, out tile);

    /// <summary>Returns <see langword="true"/> if the tile is inside bounds, currently loaded, and walkable.</summary>
    public bool IsTileWalkable(int col, int row) => TryGetLoadedTile(col, row, out Tile? tile) && tile is not null && tile.IsWalkable;

    /// <summary>Converts a world coordinate to integer grid coordinates (col, row).</summary>
    public (int Col, int Row) WorldToTile(Vector2D worldPosition)
    {
        int col = (int)MathF.Floor(worldPosition.X / _tileSize);
        int row = (int)MathF.Floor(worldPosition.Y / _tileSize);
        return (col, row);
    }

    /// <summary>Returns the world-space center point of tile (<paramref name="col"/>, <paramref name="row"/>).</summary>
    public Vector2D TileToWorldCenter(int col, int row)
    {
        return new Vector2D((col + 0.5f) * _tileSize, (row + 0.5f) * _tileSize);
    }

    /// <summary>Updates the terrain type of a specific tile owned by this island.</summary>
    public void SetTileType(int col, int row, TileType tileType)
    {
        if (!IsInBounds(col, row))
        {
            throw new ArgumentOutOfRangeException(nameof(col), $"Tile ({col}, {row}) is outside {_width}x{_height}.");
        }

        _tileTypeOverrides[new GridPoint(col, row)] = tileType;
        if (TryGetLoadedTile(col, row, out Tile? tile) && tile is not null && tile.TileType != tileType)
        {
            tile.SetTileType(tileType);
            _structureVersion++;
        }
    }

    /// <summary>Marks whether a tile is blocked by a solid placed structure.</summary>
    public void SetStructureOccupancy(int col, int row, bool isOccupied)
    {
        if (!IsInBounds(col, row))
        {
            throw new ArgumentOutOfRangeException(nameof(col), $"Tile ({col}, {row}) is outside {_width}x{_height}.");
        }

        var point = new GridPoint(col, row);
        bool changed = isOccupied
            ? _occupiedStructureTiles.Add(point)
            : _occupiedStructureTiles.Remove(point);

        if (TryGetLoadedTile(col, row, out Tile? tile) && tile is not null)
        {
            tile.SetSolidStructureOccupied(isOccupied);
        }

        if (changed)
        {
            _structureVersion++;
        }
    }

    private void RebuildLoadedTilesCache()
    {
        _loadedTilesCache.Clear();
        foreach (WorldChunk chunk in _loadedChunks.Values)
        {
            IReadOnlyList<Tile> tiles = chunk.Tiles;
            for (int i = 0; i < tiles.Count; i++)
            {
                _loadedTilesCache.Add(tiles[i]);
            }
        }

        _loadedTilesDirty = false;
    }

    private static void ValidateLevelMetadata(
        string islandId,
        string name,
        string bossType,
        int width,
        int height,
        int spawnCol,
        int spawnRow,
        float tileSize,
        int chunkSize)
    {
        if (string.IsNullOrWhiteSpace(islandId))
        {
            throw new InvalidLevelDataException("IslandId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(bossType))
        {
            throw new InvalidLevelDataException("Island name and bossType are required.", islandId);
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidLevelDataException($"Invalid island dimensions {width}x{height}.", islandId);
        }

        if (tileSize <= 0f)
        {
            throw new InvalidLevelDataException("Tile size must be greater than zero.", islandId);
        }

        if (chunkSize <= 0)
        {
            throw new InvalidLevelDataException("Chunk size must be greater than zero.", islandId);
        }

        if (spawnCol < 0 || spawnCol >= width || spawnRow < 0 || spawnRow >= height)
        {
            throw new InvalidLevelDataException($"Player spawn ({spawnCol}, {spawnRow}) is out of bounds.", islandId);
        }
    }

    private TileType DefaultIslandTerrain(int col, int row)
    {
        if (col == 0 || row == 0 || col == _width - 1 || row == _height - 1)
        {
            return TileType.Water;
        }

        if (col == 1 || row == 1 || col == _width - 2 || row == _height - 2)
        {
            return TileType.Sand;
        }

        return TileType.Grass;
    }

    private void EnsureSpawnTileIsWalkable()
    {
        Tile spawnTile = GetTile(_spawnCol, _spawnRow);
        if (!spawnTile.IsWalkable)
        {
            throw new InvalidLevelDataException(
                $"Player spawn tile ({_spawnCol}, {_spawnRow}) is not walkable.",
                _islandId);
        }
    }
}
