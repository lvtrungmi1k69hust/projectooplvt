using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;

namespace MiniNecesse.Core.World;

/// <summary>
/// Represents a fixed-size rectangular partition (typically 16x16 tiles) of an <see cref="IslandLevel"/>
/// that can be streamed into and out of memory based on player view distance.
/// </summary>
public sealed class WorldChunk
{
    private readonly Tile[] _tiles;
    private readonly IReadOnlyList<Tile> _tilesView;
    private bool _isLoaded;

    /// <summary>Gets the chunk grid coordinate.</summary>
    public ChunkCoord Coord { get; }

    /// <summary>Gets the starting global tile column index covered by this chunk.</summary>
    public int StartCol { get; }

    /// <summary>Gets the starting global tile row index covered by this chunk.</summary>
    public int StartRow { get; }

    /// <summary>Gets the width of this chunk in tiles (may be smaller at map right edge).</summary>
    public int Width { get; }

    /// <summary>Gets the height of this chunk in tiles (may be smaller at map bottom edge).</summary>
    public int Height { get; }

    /// <summary>Gets a value indicating whether this chunk is currently active/loaded in the level.</summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>Gets a cached read-only view of all tiles belonging to this chunk.</summary>
    public IReadOnlyList<Tile> Tiles => _tilesView;

    internal WorldChunk(
        ChunkCoord coord,
        int startCol,
        int startRow,
        int width,
        int height,
        Tile[] tiles)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Chunk dimensions must be positive.");
        }

        ArgumentNullException.ThrowIfNull(tiles);
        if (tiles.Length != width * height)
        {
            throw new ArgumentException($"Expected {width * height} tiles but received {tiles.Length}.", nameof(tiles));
        }

        Coord = coord;
        StartCol = startCol;
        StartRow = startRow;
        Width = width;
        Height = height;
        _tiles = tiles;
        _tilesView = Array.AsReadOnly(_tiles);
        _isLoaded = true;
    }

    /// <summary>Returns the tile at chunk-local coordinates (<paramref name="localCol"/>, <paramref name="localRow"/>).</summary>
    public Tile GetLocalTile(int localCol, int localRow)
    {
        if (localCol < 0 || localCol >= Width || localRow < 0 || localRow >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(localCol), $"Local tile ({localCol}, {localRow}) is outside chunk {Width}x{Height}.");
        }

        return _tiles[(localRow * Width) + localCol];
    }

    /// <summary>Checks whether global tile coordinates (<paramref name="col"/>, <paramref name="row"/>) fall within this chunk.</summary>
    public bool ContainsTile(int col, int row)
    {
        return col >= StartCol && col < StartCol + Width && row >= StartRow && row < StartRow + Height;
    }

    internal void MarkUnloaded() => _isLoaded = false;
}
