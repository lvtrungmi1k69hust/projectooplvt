using System;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.World;

/// <summary>
/// Immutable integer chunk coordinate on a chunk-streamed <see cref="MiniNecesse.Core.Entities.IslandLevel"/>.
/// </summary>
public readonly struct ChunkCoord : IEquatable<ChunkCoord>
{
    /// <summary>Gets the 0-based horizontal chunk index.</summary>
    public int ChunkX { get; }

    /// <summary>Gets the 0-based vertical chunk index.</summary>
    public int ChunkY { get; }

    /// <summary>Initializes a new instance of the <see cref="ChunkCoord"/> struct.</summary>
    public ChunkCoord(int chunkX, int chunkY)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
    }

    /// <summary>Converts tile grid coordinates (<paramref name="col"/>, <paramref name="row"/>) to a chunk coordinate.</summary>
    public static ChunkCoord FromTile(int col, int row, int chunkSize)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");
        }

        int cx = (int)MathF.Floor((float)col / chunkSize);
        int cy = (int)MathF.Floor((float)row / chunkSize);
        return new ChunkCoord(cx, cy);
    }

    /// <summary>Converts world-space coordinates to the enclosing chunk coordinate.</summary>
    public static ChunkCoord FromWorld(Vector2D worldPosition, float tileSize, int chunkSize)
    {
        if (tileSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), "Tile size must be positive.");
        }

        int col = (int)MathF.Floor(worldPosition.X / tileSize);
        int row = (int)MathF.Floor(worldPosition.Y / tileSize);
        return FromTile(col, row, chunkSize);
    }

    /// <summary>Computes Chebyshev (square grid ring) distance to <paramref name="other"/>.</summary>
    public int ChebyshevDistanceTo(ChunkCoord other)
    {
        return global::System.Math.Max(
            global::System.Math.Abs(ChunkX - other.ChunkX),
            global::System.Math.Abs(ChunkY - other.ChunkY));
    }

    /// <summary>Computes squared Euclidean chunk distance to <paramref name="other"/> for nearest-first ordering.</summary>
    public int SquaredDistanceTo(ChunkCoord other)
    {
        int dx = ChunkX - other.ChunkX;
        int dy = ChunkY - other.ChunkY;
        return (dx * dx) + (dy * dy);
    }

    /// <inheritdoc />
    public bool Equals(ChunkCoord other) => ChunkX == other.ChunkX && ChunkY == other.ChunkY;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ChunkX, ChunkY);

    /// <summary>Checks equality between two chunk coordinates.</summary>
    public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);

    /// <summary>Checks inequality between two chunk coordinates.</summary>
    public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({ChunkX}, {ChunkY})";
}
