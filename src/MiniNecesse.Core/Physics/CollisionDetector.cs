using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Physics;

/// <summary>
/// Performs narrow-phase AABB overlap detection, axis-separated tile sliding,
/// and anti-tunneling sub-stepped projectile collision checks backed by <see cref="SpatialHashGrid{T}"/>.
/// <para>
/// Pseudocode (Axis-Separated Tile Movement):
/// 1. Advance candidate X position; inspect overlapping grid tiles; if any tile is non-walkable, clamp X flush to tile edge.
/// 2. Advance candidate Y position using resolved X; inspect overlapping grid tiles; clamp Y flush to tile edge.
/// 3. Clamp final position within island world bounds.
/// </para>
/// <para>
/// Pseudocode (Anti-Tunneling Swept Projectile Step):
/// 1. Compute full frame displacement D = velocity * dt.
/// 2. Subdivide D into steps = ceil(|D| / (0.5 * min(width, height))) to prevent fast bullets skipping thin walls.
/// 3. At each sub-step: test tilemap walkability and query SpatialHashGrid for AABB hits; stop at first impact.
/// </para>
/// <para>
/// Complexity: O(1) per entity tile slide; O(S * k) per projectile swept step where S is sub-step count;
/// O(n) total broad-phase across n entities using <see cref="SpatialHashGrid{T}"/>.
/// </para>
/// <para>
/// Edge cases: Eliminates wall corner snagging via axis separation + skin epsilon (0.01f);
/// prevents projectile tunneling on high velocity; safely clamps at island edges.
/// </para>
/// </summary>
public sealed class CollisionDetector
{
    private const float SKIN_EPSILON = 0.01f;
    private const int MAX_SWEPT_SUBSTEPS = 16;

    /// <summary>
    /// Checks exact AABB overlap between two entities and computes separation normal and penetration depth.
    /// Complexity: O(1).
    /// </summary>
    public bool TryDetectOverlap(Entity entityA, Entity entityB, out CollisionInfo collisionInfo)
    {
        ArgumentNullException.ThrowIfNull(entityA);
        ArgumentNullException.ThrowIfNull(entityB);

        collisionInfo = default;
        if (!entityA.IsActive || !entityB.IsActive || ReferenceEquals(entityA, entityB))
        {
            return false;
        }

        RectF boundsA = entityA.GetBounds();
        RectF boundsB = entityB.GetBounds();
        if (!boundsA.Intersects(boundsB))
        {
            return false;
        }

        RectF overlap = boundsA.GetIntersection(boundsB);
        (Vector2D normal, float depth) = ComputeMinimumSeparation(boundsA, boundsB, overlap);
        collisionInfo = new CollisionInfo(entityA.Id, entityB.Id, overlap, normal, depth);
        return true;
    }

    /// <summary>
    /// Moves a character/movable entity by <paramref name="delta"/> using axis-separated sliding collision
    /// against non-walkable tiles and island boundaries to prevent sticking on tile corners.
    /// Complexity: O(1) (checks only tiles overlapped by the entity footprint).
    /// </summary>
    public Vector2D MoveWithTileCollision(IMovable movable, Vector2D delta, Vector2D entitySize, IslandLevel level)
    {
        ArgumentNullException.ThrowIfNull(movable);
        ArgumentNullException.ThrowIfNull(level);

        Vector2D resolvedX = ResolveHorizontalStep(movable.Position, delta.X, entitySize, level);
        Vector2D resolvedXY = ResolveVerticalStep(resolvedX, delta.Y, entitySize, level);
        Vector2D clamped = ClampToWorldBounds(resolvedXY, entitySize, level);

        movable.SetPosition(clamped);
        return clamped;
    }

    /// <summary>
    /// Advances a <paramref name="projectile"/> across <paramref name="deltaTime"/> using sub-stepped swept checks
    /// to prevent high-speed projectiles from tunneling through thin walls or small targets.
    /// Returns <see langword="true"/> if an obstacle or entity was hit.
    /// </summary>
    public bool TryAdvanceProjectileSwept<TTarget>(
        Projectile projectile,
        float deltaTime,
        IslandLevel level,
        SpatialHashGrid<TTarget> spatialGrid,
        List<TTarget> candidateBuffer,
        out TTarget? hitEntity,
        out CollisionInfo collisionInfo)
        where TTarget : Entity
    {
        ArgumentNullException.ThrowIfNull(projectile);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(spatialGrid);
        ArgumentNullException.ThrowIfNull(candidateBuffer);

        hitEntity = null;
        collisionInfo = default;
        if (!projectile.IsActive || deltaTime <= 0f)
        {
            return false;
        }

        int steps = CalculateSubStepCount(projectile, deltaTime);
        float subDt = deltaTime / steps;

        for (int step = 0; step < steps && projectile.IsActive; step++)
        {
            projectile.Update(subDt);
            if (!projectile.IsActive)
            {
                return false;
            }

            if (HitsSolidTileOrBoundary(projectile.GetBounds(), level))
            {
                projectile.Deactivate();
                return true;
            }

            if (TryHitSpatialCandidate(projectile, spatialGrid, candidateBuffer, out hitEntity, out collisionInfo))
            {
                return true;
            }
        }

        return false;
    }

    private static (Vector2D Normal, float Depth) ComputeMinimumSeparation(RectF a, RectF b, RectF overlap)
    {
        if (overlap.Width < overlap.Height)
        {
            float signX = a.Center.X < b.Center.X ? -1f : 1f;
            return (new Vector2D(signX, 0f), overlap.Width);
        }

        float signY = a.Center.Y < b.Center.Y ? -1f : 1f;
        return (new Vector2D(0f, signY), overlap.Height);
    }

    private static Vector2D ResolveHorizontalStep(Vector2D startPos, float deltaX, Vector2D size, IslandLevel level)
    {
        if (MathF.Abs(deltaX) <= 1e-6f)
        {
            return startPos;
        }

        float candidateX = startPos.X + deltaX;
        var candidateBounds = new RectF(candidateX, startPos.Y, size.X, size.Y);
        if (!HitsSolidTileOrBoundary(candidateBounds, level))
        {
            return new Vector2D(candidateX, startPos.Y);
        }

        float tileSize = level.TileSize;
        if (deltaX > 0f)
        {
            int hitCol = (int)MathF.Floor((candidateBounds.Right - SKIN_EPSILON) / tileSize);
            candidateX = (hitCol * tileSize) - size.X - SKIN_EPSILON;
        }
        else
        {
            int hitCol = (int)MathF.Floor(candidateBounds.Left / tileSize);
            candidateX = ((hitCol + 1) * tileSize) + SKIN_EPSILON;
        }

        return new Vector2D(candidateX, startPos.Y);
    }

    private static Vector2D ResolveVerticalStep(Vector2D startPos, float deltaY, Vector2D size, IslandLevel level)
    {
        if (MathF.Abs(deltaY) <= 1e-6f)
        {
            return startPos;
        }

        float candidateY = startPos.Y + deltaY;
        var candidateBounds = new RectF(startPos.X, candidateY, size.X, size.Y);
        if (!HitsSolidTileOrBoundary(candidateBounds, level))
        {
            return new Vector2D(startPos.X, candidateY);
        }

        float tileSize = level.TileSize;
        if (deltaY > 0f)
        {
            int hitRow = (int)MathF.Floor((candidateBounds.Bottom - SKIN_EPSILON) / tileSize);
            candidateY = (hitRow * tileSize) - size.Y - SKIN_EPSILON;
        }
        else
        {
            int hitRow = (int)MathF.Floor(candidateBounds.Top / tileSize);
            candidateY = ((hitRow + 1) * tileSize) + SKIN_EPSILON;
        }

        return new Vector2D(startPos.X, candidateY);
    }

    private static Vector2D ClampToWorldBounds(Vector2D position, Vector2D size, IslandLevel level)
    {
        float maxX = MathF.Max(0f, level.WorldWidth - size.X);
        float maxY = MathF.Max(0f, level.WorldHeight - size.Y);
        return new Vector2D(
            global::System.Math.Clamp(position.X, 0f, maxX),
            global::System.Math.Clamp(position.Y, 0f, maxY));
    }

    private static bool HitsSolidTileOrBoundary(RectF bounds, IslandLevel level)
    {
        if (bounds.Left < 0f || bounds.Top < 0f || bounds.Right > level.WorldWidth || bounds.Bottom > level.WorldHeight)
        {
            return true;
        }

        int minCol = (int)MathF.Floor(bounds.Left / level.TileSize);
        int maxCol = (int)MathF.Floor((bounds.Right - SKIN_EPSILON) / level.TileSize);
        int minRow = (int)MathF.Floor(bounds.Top / level.TileSize);
        int maxRow = (int)MathF.Floor((bounds.Bottom - SKIN_EPSILON) / level.TileSize);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int col = minCol; col <= maxCol; col++)
            {
                if (!level.IsTileWalkable(col, row))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CalculateSubStepCount(Projectile projectile, float deltaTime)
    {
        float travelDistance = (projectile.Velocity * deltaTime).Length;
        float halfMinExtent = MathF.Min(projectile.Size.X, projectile.Size.Y) * 0.5f;
        if (halfMinExtent <= SKIN_EPSILON || travelDistance <= halfMinExtent)
        {
            return 1;
        }

        int steps = (int)MathF.Ceiling(travelDistance / halfMinExtent);
        return global::System.Math.Clamp(steps, 1, MAX_SWEPT_SUBSTEPS);
    }

    private bool TryHitSpatialCandidate<TTarget>(
        Projectile projectile,
        SpatialHashGrid<TTarget> spatialGrid,
        List<TTarget> candidateBuffer,
        out TTarget? hitEntity,
        out CollisionInfo collisionInfo)
        where TTarget : Entity
    {
        hitEntity = null;
        collisionInfo = default;

        spatialGrid.Query(projectile.GetBounds(), candidateBuffer);
        for (int i = 0; i < candidateBuffer.Count; i++)
        {
            TTarget candidate = candidateBuffer[i];
            if (string.Equals(candidate.Id, projectile.OwnerId, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryDetectOverlap(projectile, candidate, out collisionInfo))
            {
                hitEntity = candidate;
                return true;
            }
        }

        return false;
    }
}
