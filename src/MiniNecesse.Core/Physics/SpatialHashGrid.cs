using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Physics;

/// <summary>
/// Uniform 2D spatial hash grid accelerating broad-phase collision queries from O(n^2) to near O(n).
/// <para>
/// Pseudocode:
/// 1. Map any bounding box [Left..Right, Top..Bottom] to integer cell range [minX..maxX, minY..maxY].
/// 2. Insert(entity): append entity reference to every overlapped cell bucket.
/// 3. Query(area, buffer): scan overlapped cell buckets and deduplicate via a reusable hash set.
/// 4. Clear(): clear existing bucket lists in place without releasing bucket instances (zero GC churn).
/// </para>
/// <para>
/// Complexity: Insert O(k) where k is cells spanned by entity (typically 1..4);
/// Broad-phase collision pass O(n) for uniformly distributed entities vs O(n^2) brute force.
/// </para>
/// <para>
/// Edge cases: Handles negative coordinates via MathF.Floor, oversized entities spanning multiple cells,
/// and empty query rectangles cleanly without throwing.
/// </para>
/// </summary>
/// <typeparam name="T">Entity subtype indexed in the grid.</typeparam>
public sealed class SpatialHashGrid<T> where T : Entity
{
    private readonly float _cellSize;
    private readonly Dictionary<(int CellX, int CellY), List<T>> _buckets = new();
    private readonly List<List<T>> _usedBucketList = new();
    private readonly HashSet<T> _dedupBuffer = new();

    /// <summary>Gets the uniform world-space width and height of each spatial cell.</summary>
    public float CellSize => _cellSize;

    /// <summary>Gets the number of active non-empty buckets currently tracked.</summary>
    public int ActiveBucketCount => _usedBucketList.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpatialHashGrid{T}"/> class.
    /// </summary>
    /// <param name="cellSize">Positive cell dimension in world units (recommended 2-4x tile size).</param>
    public SpatialHashGrid(float cellSize = 96f)
    {
        if (cellSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "Spatial grid cell size must be positive.");
        }

        _cellSize = cellSize;
    }

    /// <summary>
    /// Clears all entities from buckets while retaining list capacities to avoid per-frame GC allocations.
    /// Complexity: O(B) where B is the number of touched buckets.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _usedBucketList.Count; i++)
        {
            _usedBucketList[i].Clear();
        }

        _usedBucketList.Clear();
    }

    /// <summary>
    /// Inserts an active entity into all grid cells intersecting its AABB.
    /// Complexity: O(k), where k is the number of cells overlapped by the entity bounds.
    /// </summary>
    public void Insert(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!entity.IsActive)
        {
            return;
        }

        RectF bounds = entity.GetBounds();
        (int minX, int maxX, int minY, int maxY) = ComputeCellRange(bounds);

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                AddToBucket((cx, cy), entity);
            }
        }
    }

    /// <summary>
    /// Inserts all active entities from <paramref name="entities"/> into the grid.
    /// Complexity: O(n).
    /// </summary>
    public void InsertRange(IReadOnlyList<T> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        for (int i = 0; i < entities.Count; i++)
        {
            Insert(entities[i]);
        }
    }

    /// <summary>
    /// Writes all unique candidate entities overlapping <paramref name="queryArea"/> into <paramref name="resultsBuffer"/>.
    /// Clears <paramref name="resultsBuffer"/> first and performs zero heap allocations during steady-state gameplay.
    /// Complexity: O(k + m), where k is overlapped cells and m is entities in those cells.
    /// </summary>
    public void Query(RectF queryArea, List<T> resultsBuffer)
    {
        ArgumentNullException.ThrowIfNull(resultsBuffer);
        resultsBuffer.Clear();
        if (queryArea.IsEmpty)
        {
            return;
        }

        _dedupBuffer.Clear();
        (int minX, int maxX, int minY, int maxY) = ComputeCellRange(queryArea);

        for (int cy = minY; cy <= maxY; cy++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                CollectBucketCandidates((cx, cy), resultsBuffer);
            }
        }
    }

    private (int MinX, int MaxX, int MinY, int MaxY) ComputeCellRange(RectF bounds)
    {
        int minX = (int)MathF.Floor(bounds.Left / _cellSize);
        int maxX = (int)MathF.Floor(bounds.Right / _cellSize);
        int minY = (int)MathF.Floor(bounds.Top / _cellSize);
        int maxY = (int)MathF.Floor(bounds.Bottom / _cellSize);
        return (minX, maxX, minY, maxY);
    }

    private void AddToBucket((int CellX, int CellY) key, T entity)
    {
        if (!_buckets.TryGetValue(key, out List<T>? bucket))
        {
            bucket = new List<T>(8);
            _buckets[key] = bucket;
        }

        if (bucket.Count == 0)
        {
            _usedBucketList.Add(bucket);
        }

        bucket.Add(entity);
    }

    private void CollectBucketCandidates((int CellX, int CellY) key, List<T> resultsBuffer)
    {
        if (!_buckets.TryGetValue(key, out List<T>? bucket) || bucket.Count == 0)
        {
            return;
        }

        for (int i = 0; i < bucket.Count; i++)
        {
            T candidate = bucket[i];
            if (candidate.IsActive && _dedupBuffer.Add(candidate))
            {
                resultsBuffer.Add(candidate);
            }
        }
    }
}
