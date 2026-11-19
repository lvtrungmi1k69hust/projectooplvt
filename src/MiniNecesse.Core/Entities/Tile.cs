using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Flyweight;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a single grid cell exclusively created and owned by <see cref="IslandLevel"/> (UML Composition).
/// Stores only extrinsic per-cell coordinates and occupancy while sharing intrinsic terrain metadata via
/// <see cref="TileDataFlyweight"/> (GoF Flyweight).
/// </summary>
public sealed class Tile
{
    private readonly int _col;
    private readonly int _row;
    private TileDataFlyweight _flyweight;
    private bool _isOccupiedBySolidStructure;

    /// <summary>Gets the 0-based grid column index.</summary>
    public int Col => _col;

    /// <summary>Gets the 0-based grid row index.</summary>
    public int Row => _row;

    /// <summary>Gets the shared intrinsic flyweight describing terrain properties.</summary>
    public TileDataFlyweight Flyweight => _flyweight;

    /// <summary>Gets the terrain type of this tile.</summary>
    public TileType TileType => _flyweight.TileType;

    /// <summary>Gets the terrain hardness from the shared flyweight.</summary>
    public int Hardness => _flyweight.Hardness;

    /// <summary>Gets the logical sprite identifier from the shared flyweight.</summary>
    public string SpriteId => _flyweight.SpriteId;

    /// <summary>Gets the hex base color from the shared flyweight.</summary>
    public string ColorHex => _flyweight.ColorHex;

    /// <summary>Gets a value indicating whether a solid structure currently blocks this tile.</summary>
    public bool IsOccupiedBySolidStructure => _isOccupiedBySolidStructure;

    /// <summary>Gets a value indicating whether characters can traverse this tile.</summary>
    public bool IsWalkable => _flyweight.IsWalkable && !_isOccupiedBySolidStructure;

    /// <summary>Gets the terrain movement speed factor from the shared flyweight.</summary>
    public float MovementSpeedMultiplier => _flyweight.MovementSpeedMultiplier;

    /// <summary>
    /// Internal constructor enforcing UML Composition: only <see cref="IslandLevel"/> constructs tiles.
    /// </summary>
    internal Tile(int col, int row, TileType tileType, TileDataFlyweightFactory? flyweightFactory = null)
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
        _flyweight = (flyweightFactory ?? TileDataFlyweightFactory.Shared).Get(tileType);
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

    /// <summary>Updates the terrain type of this tile by swapping its shared flyweight reference.</summary>
    internal void SetTileType(TileType tileType, TileDataFlyweightFactory? flyweightFactory = null)
    {
        _flyweight = (flyweightFactory ?? TileDataFlyweightFactory.Shared).Get(tileType);
    }

    /// <summary>Marks whether a solid structure occupies this tile.</summary>
    internal void SetSolidStructureOccupied(bool isOccupied) => _isOccupiedBySolidStructure = isOccupied;
}
