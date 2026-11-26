using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.World;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Caches pre-rendered terrain <see cref="Bitmap"/> textures at native 1:1 tile pixel size (e.g., 512x512 in opaque Rgb format)
/// per loaded <see cref="WorldChunk"/> to replace ~700-1,400 per-tile GDI+ calls with ~4-9 fast unscaled blits.
/// Bounds memory via LRU eviction (max 32 chunks) and disposes unloaded chunk bitmaps automatically.
/// </summary>
public sealed class ChunkTerrainCache : IDisposable
{
    private const int MAX_CACHED_CHUNKS = 32;

    private sealed class CachedChunkEntry : IDisposable
    {
        public Bitmap Frame0 = null!;
        public Bitmap? Frame1;
        public byte NeighborLoadedMask;
        public int TilePixelSize;
        public long LastUsedSequence;

        public void Dispose()
        {
            Frame0?.Dispose();
            Frame1?.Dispose();
        }
    }

    private readonly Dictionary<ChunkCoord, CachedChunkEntry> _entries = new();
    private readonly List<ChunkCoord> _evictBuffer = new(16);
    private int _lastPrunedVersion = -1;
    private long _useSequence;
    private bool _disposed;

    /// <summary>Gets the count of chunk bitmaps currently held in the render cache.</summary>
    public int CachedChunkCount => _entries.Count;

    /// <summary>
    /// Removes and disposes cached bitmaps belonging to chunks that are no longer loaded.
    /// </summary>
    public void PruneUnloaded(IslandLevel level)
    {
        if (_disposed) return;

        if (_lastPrunedVersion == level.StructureVersion && _entries.Count <= level.LoadedChunkCount)
        {
            return;
        }

        _lastPrunedVersion = level.StructureVersion;
        _evictBuffer.Clear();

        foreach (ChunkCoord coord in _entries.Keys)
        {
            if (!level.IsChunkLoaded(coord))
            {
                _evictBuffer.Add(coord);
            }
        }

        for (int i = 0; i < _evictBuffer.Count; i++)
        {
            ChunkCoord deadCoord = _evictBuffer[i];
            if (_entries.Remove(deadCoord, out CachedChunkEntry? entry))
            {
                entry.Dispose();
            }
        }
    }

    /// <summary>
    /// Retrieves or bakes the native-resolution opaque terrain <see cref="Bitmap"/> for <paramref name="chunk"/> at <paramref name="waterFrame"/> (0 or 1).
    /// </summary>
    public Bitmap GetOrBuild(
        IslandLevel level,
        WorldChunk chunk,
        int waterFrame,
        SpriteAtlasCache sprites,
        RenderResourceCache cache)
    {
        int tilePx = Math.Max(8, (int)MathF.Round(level.TileSize));
        byte neighborMask = ComputeNeighborLoadedMask(level, chunk.Coord);

        if (_entries.TryGetValue(chunk.Coord, out CachedChunkEntry? existing) &&
            existing.NeighborLoadedMask == neighborMask &&
            existing.TilePixelSize == tilePx)
        {
            existing.LastUsedSequence = ++_useSequence;
            return waterFrame == 1 && existing.Frame1 is not null
                ? existing.Frame1
                : existing.Frame0;
        }

        if (existing is not null)
        {
            existing.Dispose();
            _entries.Remove(chunk.Coord);
        }
        else if (_entries.Count >= MAX_CACHED_CHUNKS)
        {
            EvictLeastRecentlyUsed();
        }

        CachedChunkEntry rebuilt = BuildEntry(level, chunk, tilePx, neighborMask, sprites, cache);
        rebuilt.LastUsedSequence = ++_useSequence;
        _entries[chunk.Coord] = rebuilt;

        return waterFrame == 1 && rebuilt.Frame1 is not null
            ? rebuilt.Frame1
            : rebuilt.Frame0;
    }

    /// <summary>Disposes all cached chunk bitmaps immediately (e.g., on island change).</summary>
    public void Clear()
    {
        foreach (CachedChunkEntry entry in _entries.Values)
        {
            entry.Dispose();
        }

        _entries.Clear();
        _evictBuffer.Clear();
        _lastPrunedVersion = -1;
        _useSequence = 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
    }

    private void EvictLeastRecentlyUsed()
    {
        ChunkCoord oldestCoord = default;
        long oldestSeq = long.MaxValue;
        bool found = false;

        foreach (KeyValuePair<ChunkCoord, CachedChunkEntry> kvp in _entries)
        {
            if (kvp.Value.LastUsedSequence < oldestSeq)
            {
                oldestSeq = kvp.Value.LastUsedSequence;
                oldestCoord = kvp.Key;
                found = true;
            }
        }

        if (found && _entries.Remove(oldestCoord, out CachedChunkEntry? victim))
        {
            victim.Dispose();
        }
    }

    private static CachedChunkEntry BuildEntry(
        IslandLevel level,
        WorldChunk chunk,
        int tilePx,
        byte neighborMask,
        SpriteAtlasCache sprites,
        RenderResourceCache cache)
    {
        int pxW = chunk.Width * tilePx;
        int pxH = chunk.Height * tilePx;

        bool hasWater = ChunkContainsWater(chunk);
        var frame0 = new Bitmap(pxW, pxH, PixelFormat.Format32bppRgb);
        BakeChunkFrame(frame0, level, chunk, tilePx, waterFrame: 0, hasWater, sprites, cache);

        Bitmap? frame1 = null;
        if (hasWater)
        {
            frame1 = new Bitmap(pxW, pxH, PixelFormat.Format32bppRgb);
            BakeChunkFrame(frame1, level, chunk, tilePx, waterFrame: 1, hasWater: true, sprites, cache);
        }

        return new CachedChunkEntry
        {
            Frame0 = frame0,
            Frame1 = frame1,
            NeighborLoadedMask = neighborMask,
            TilePixelSize = tilePx
        };
    }

    private static void BakeChunkFrame(
        Bitmap target,
        IslandLevel level,
        WorldChunk chunk,
        int tilePx,
        int waterFrame,
        bool hasWater,
        SpriteAtlasCache sprites,
        RenderResourceCache cache)
    {
        using Graphics cg = Graphics.FromImage(target);
        cg.CompositingQuality = CompositingQuality.HighSpeed;
        cg.InterpolationMode = InterpolationMode.NearestNeighbor;
        cg.PixelOffsetMode = PixelOffsetMode.Half;
        cg.SmoothingMode = SmoothingMode.None;
        cg.CompositingMode = CompositingMode.SourceCopy;

        for (int r = 0; r < chunk.Height; r++)
        {
            for (int c = 0; c < chunk.Width; c++)
            {
                Tile tile = chunk.GetLocalTile(c, r);
                Bitmap sprite = sprites.GetTileSprite(tile.TileType, tile.Col, tile.Row, waterFrame);
                cg.DrawImage(sprite, c * tilePx, r * tilePx, tilePx, tilePx);
            }
        }

        if (!hasWater)
        {
            return;
        }

        cg.CompositingMode = CompositingMode.SourceOver;
        float foamThickness = Math.Max(2f, tilePx * (3f / 32f));
        for (int r = 0; r < chunk.Height; r++)
        {
            for (int c = 0; c < chunk.Width; c++)
            {
                Tile tile = chunk.GetLocalTile(c, r);
                if (tile.TileType != TileType.Water)
                {
                    continue;
                }

                float sx = c * tilePx;
                float sy = r * tilePx;
                if (IsLandTile(level, tile.Col, tile.Row - 1)) cg.FillRectangle(cache.ShoreFoamBrush, sx, sy, tilePx, foamThickness);
                if (IsLandTile(level, tile.Col, tile.Row + 1)) cg.FillRectangle(cache.ShoreFoamBrush, sx, sy + tilePx - foamThickness, tilePx, foamThickness);
                if (IsLandTile(level, tile.Col - 1, tile.Row)) cg.FillRectangle(cache.ShoreFoamBrush, sx, sy, foamThickness, tilePx);
                if (IsLandTile(level, tile.Col + 1, tile.Row)) cg.FillRectangle(cache.ShoreFoamBrush, sx + tilePx - foamThickness, sy, foamThickness, tilePx);
            }
        }
    }

    private static bool ChunkContainsWater(WorldChunk chunk)
    {
        for (int r = 0; r < chunk.Height; r++)
        {
            for (int c = 0; c < chunk.Width; c++)
            {
                if (chunk.GetLocalTile(c, r).TileType == TileType.Water)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static byte ComputeNeighborLoadedMask(IslandLevel level, ChunkCoord coord)
    {
        byte mask = 0;
        if (IsChunkLoadedOrOob(level, coord.ChunkX, coord.ChunkY - 1)) mask |= 1;
        if (IsChunkLoadedOrOob(level, coord.ChunkX, coord.ChunkY + 1)) mask |= 2;
        if (IsChunkLoadedOrOob(level, coord.ChunkX - 1, coord.ChunkY)) mask |= 4;
        if (IsChunkLoadedOrOob(level, coord.ChunkX + 1, coord.ChunkY)) mask |= 8;
        return mask;
    }

    private static bool IsChunkLoadedOrOob(IslandLevel level, int cx, int cy)
    {
        var neighbor = new ChunkCoord(cx, cy);
        return !level.IsChunkInBounds(neighbor) || level.IsChunkLoaded(neighbor);
    }

    private static bool IsLandTile(IslandLevel level, int col, int row)
    {
        return level.TryGetLoadedTile(col, row, out Tile? neighbor) && neighbor is not null && neighbor.TileType != TileType.Water;
    }
}
