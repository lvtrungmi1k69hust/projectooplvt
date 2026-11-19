using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a 2D top-down island tilemap.
/// Demonstrates UML Composition by internally creating and exclusively owning its <see cref="Tile"/> collection.
/// </summary>
public sealed class IslandLevel
{
    private readonly string _islandId;
    private readonly string _name;
    private readonly string _biome;
    private readonly string _bossType;
    private readonly int _width;
    private readonly int _height;
    private readonly float _tileSize;
    private readonly int _spawnCol;
    private readonly int _spawnRow;
    private readonly List<Tile> _tiles;

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

    /// <summary>Gets a read-only view of all tiles owned by this island.</summary>
    public IReadOnlyList<Tile> Tiles => _tiles.AsReadOnly();

    /// <summary>
    /// Initializes a new <see cref="IslandLevel"/> and internally generates its composed <see cref="Tile"/> grid.
    /// </summary>
    /// <exception cref="InvalidLevelDataException">Thrown when dimensions, identifiers, or spawn coordinates are invalid.</exception>
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
        Func<int, int, TileType>? terrainGenerator = null)
    {
        ValidateLevelMetadata(islandId, name, bossType, width, height, spawnCol, spawnRow, tileSize);

        _islandId = islandId;
        _name = name;
        _biome = string.IsNullOrWhiteSpace(biome) ? "Forest" : biome;
        _bossType = bossType;
        _width = width;
        _height = height;
        _spawnCol = spawnCol;
        _spawnRow = spawnRow;
        _tileSize = tileSize;
        _tiles = BuildComposedTiles(width, height, terrainGenerator ?? DefaultIslandTerrain);

        EnsureSpawnTileIsWalkable();
    }

    /// <summary>Checks whether (<paramref name="col"/>, <paramref name="row"/>) lies inside the island grid.</summary>
    public bool IsInBounds(int col, int row) => col >= 0 && col < _width && row >= 0 && row < _height;

    /// <summary>Returns the tile at (<paramref name="col"/>, <paramref name="row"/>).</summary>
    public Tile GetTile(int col, int row)
    {
        if (!IsInBounds(col, row))
        {
            throw new ArgumentOutOfRangeException(nameof(col), $"Tile ({col}, {row}) is outside {_width}x{_height}.");
        }

        return _tiles[(row * _width) + col];
    }

    /// <summary>Attempts to retrieve the tile at (<paramref name="col"/>, <paramref name="row"/>) without throwing.</summary>
    public bool TryGetTile(int col, int row, out Tile? tile)
    {
        if (!IsInBounds(col, row))
        {
            tile = null;
            return false;
        }

        tile = _tiles[(row * _width) + col];
        return true;
    }

    /// <summary>Returns <see langword="true"/> if the tile is inside bounds and currently walkable.</summary>
    public bool IsTileWalkable(int col, int row) => TryGetTile(col, row, out Tile? tile) && tile is not null && tile.IsWalkable;

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
    public void SetTileType(int col, int row, TileType tileType) => GetTile(col, row).SetTileType(tileType);

    /// <summary>Marks whether a tile is blocked by a solid placed structure.</summary>
    public void SetStructureOccupancy(int col, int row, bool isOccupied) => GetTile(col, row).SetSolidStructureOccupied(isOccupied);

    private static void ValidateLevelMetadata(
        string islandId,
        string name,
        string bossType,
        int width,
        int height,
        int spawnCol,
        int spawnRow,
        float tileSize)
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

        if (spawnCol < 0 || spawnCol >= width || spawnRow < 0 || spawnRow >= height)
        {
            throw new InvalidLevelDataException($"Player spawn ({spawnCol}, {spawnRow}) is out of bounds.", islandId);
        }
    }

    private static List<Tile> BuildComposedTiles(int width, int height, Func<int, int, TileType> generator)
    {
        var tiles = new List<Tile>(width * height);
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                tiles.Add(new Tile(col, row, generator(col, row)));
            }
        }

        return tiles;
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
