using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a single grid cell exclusively created and owned by <see cref="IslandLevel"/> (Composition).
/// </summary>
public sealed class Tile
{
    private readonly int _col;
    private readonly int _row;
    private TileType _tileType;
    private bool _isOccupiedBySolidStructure;

    /// <summary>Gets the 0-based grid column index.</summary>
    public int Col => _col;

    /// <summary>Gets the 0-based grid row index.</summary>
    public int Row => _row;

    /// <summary>Gets the terrain type of this tile.</summary>
    public TileType TileType => _tileType;

    /// <summary>Gets a value indicating whether a solid structure currently blocks this tile.</summary>
    public bool IsOccupiedBySolidStructure => _isOccupiedBySolidStructure;

    /// <summary>Gets a value indicating whether characters can traverse this tile.</summary>
    public bool IsWalkable => _tileType != TileType.Water && !_isOccupiedBySolidStructure;

    /// <summary>Gets the terrain movement speed factor (e.g., slower on swamp tiles).</summary>
    public float MovementSpeedMultiplier => ResolveSpeedMultiplier(_tileType);

    /// <summary>
    /// Internal constructor enforcing UML Composition: only <see cref="IslandLevel"/> constructs tiles.
    /// </summary>
    internal Tile(int col, int row, TileType tileType)
    {
        if (col < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(col), "Tile column cannot be negative.");
        }

        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "Tile row cannot be negative.");
        }

        _col = col;
        _row = row;
        _tileType = tileType;
    }

    /// <summary>Computes the world-space bounding rectangle of this tile.</summary>
    public RectF GetWorldBounds(float tileSize)
    {
        if (tileSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be positive.");
        }

        return new RectF(_col * tileSize, _row * tileSize, tileSize, tileSize);
    }

    /// <summary>Updates the terrain type of this tile.</summary>
    internal void SetTileType(TileType tileType) => _tileType = tileType;

    /// <summary>Marks whether a solid structure occupies this tile.</summary>
    internal void SetSolidStructureOccupied(bool isOccupied) => _isOccupiedBySolidStructure = isOccupied;

    private static float ResolveSpeedMultiplier(TileType tileType)
    {
        return tileType switch
        {
            TileType.Water => 0f,
            TileType.Swamp => 0.72f,
            TileType.Snow => 0.88f,
            TileType.Sand => 0.94f,
            _ => 1.0f
        };
    }
}
