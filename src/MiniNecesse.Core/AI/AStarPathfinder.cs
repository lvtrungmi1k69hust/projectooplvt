using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;

namespace MiniNecesse.Core.AI;

/// <summary>
/// Immutable integer grid coordinate on an <see cref="IslandLevel"/> tilemap.
/// </summary>
public readonly struct GridPoint : IEquatable<GridPoint>
{
    /// <summary>Gets the 0-based grid column.</summary>
    public int Col { get; }

    /// <summary>Gets the 0-based grid row.</summary>
    public int Row { get; }

    /// <summary>Initializes a new instance of the <see cref="GridPoint"/> struct.</summary>
    public GridPoint(int col, int row)
    {
        Col = col;
        Row = row;
    }

    /// <inheritdoc />
    public bool Equals(GridPoint other) => Col == other.Col && Row == other.Row;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Col, Row);

    /// <summary>Checks equality between two grid points.</summary>
    public static bool operator ==(GridPoint left, GridPoint right) => left.Equals(right);

    /// <summary>Checks inequality between two grid points.</summary>
    public static bool operator !=(GridPoint left, GridPoint right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({Col}, {Row})";
}

/// <summary>
/// Outcome returned by <see cref="AStarPathfinder"/> including reached status and optional blocking wall target.
/// </summary>
public readonly struct PathResult
{
    /// <summary>Gets a value indicating whether an unobstructed walkable path reached the goal tile.</summary>
    public bool ReachedGoal { get; }

    /// <summary>Gets the number of waypoints written into the path buffer.</summary>
    public int WaypointCount { get; }

    /// <summary>Gets the tile of a player-built blocking wall if the goal is completely walled off.</summary>
    public GridPoint? BlockingWallTile { get; }

    /// <summary>Gets a value indicating whether a destructible wall blocks access to the goal.</summary>
    public bool HasBlockingWall => BlockingWallTile.HasValue;

    /// <summary>Initializes a new instance of the <see cref="PathResult"/> struct.</summary>
    public PathResult(bool reachedGoal, int waypointCount, GridPoint? blockingWallTile = null)
    {
        ReachedGoal = reachedGoal;
        WaypointCount = waypointCount;
        BlockingWallTile = blockingWallTile;
    }
}

/// <summary>
/// Implements A* shortest-path search over an <see cref="IslandLevel"/> tile grid using Manhattan heuristic.
/// <para>
/// Pseudocode:
/// 1. Validate start and goal bounds; return empty result safely if invalid.
/// 2. Initialize open PriorityQueue with start (g=0, f=Manhattan(start, goal)), gScore map, cameFrom map, and closestReached tracker.
/// 3. Loop while open set is non-empty and expansions &lt; maxIterations:
///    a. Pop node with minimum fScore; skip stale entries in closed set.
///    b. If current == goal, reconstruct path from cameFrom and return ReachedGoal=true.
///    c. Track best node closest to goal by Manhattan distance.
///    d. Relax 4 cardinal neighbors: if walkable and tentativeG &lt; gScore[neighbor], push to PriorityQueue.
/// 4. Edge-case fallback (completely walled in): if goal was unreachable, reconstruct path to closestReached
///    and locate any adjacent tile occupied by a destructible solid structure so AI can breach the wall.
/// </para>
/// <para>
/// Complexity: Time O((V + E) log V), Space O(V) where V = Width * Height and E &lt;= 4V.
/// </para>
/// <para>
/// Edge cases: Out-of-bounds start/goal returns safely; non-walkable goal falls back to nearest reachable tile or wall;
/// completely enclosed player triggers wall-breach targeting; bounded maxIterations (default 500) guarantees frame safety.
/// </para>
/// </summary>
public sealed class AStarPathfinder
{
    private const int DEFAULT_MAX_ITERATIONS = 500;

    private static readonly GridPoint[] CARDINAL_OFFSETS =
    {
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0)
    };

    private readonly PriorityQueue<GridPoint, int> _openSet = new();
    private readonly Dictionary<GridPoint, int> _gScores = new();
    private readonly Dictionary<GridPoint, GridPoint> _cameFrom = new();
    private readonly HashSet<GridPoint> _closedSet = new();

    /// <summary>
    /// Computes the Manhattan distance heuristic h(a, b) = |a.Col - b.Col| + |a.Row - b.Row|.
    /// </summary>
    public static int ComputeManhattan(GridPoint a, GridPoint b)
    {
        return global::System.Math.Abs(a.Col - b.Col) + global::System.Math.Abs(a.Row - b.Row);
    }

    /// <summary>
    /// Finds a shortest path from <paramref name="start"/> to <paramref name="goal"/> and writes waypoints
    /// into caller-supplied <paramref name="outputBuffer"/> without allocating a new list.
    /// </summary>
    public PathResult FindPath(
        IslandLevel level,
        GridPoint start,
        GridPoint goal,
        List<GridPoint> outputBuffer,
        int maxIterations = DEFAULT_MAX_ITERATIONS)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(outputBuffer);
        outputBuffer.Clear();

        if (!level.IsInBounds(start.Col, start.Row) || !level.IsInBounds(goal.Col, goal.Row) || maxIterations <= 0)
        {
            return new PathResult(reachedGoal: false, waypointCount: 0);
        }

        if (start == goal)
        {
            outputBuffer.Add(start);
            return new PathResult(reachedGoal: true, waypointCount: 1);
        }

        ResetScratchBuffers();
        return RunAStarSearch(level, start, goal, outputBuffer, maxIterations);
    }

    /// <summary>
    /// Convenience overload returning a newly allocated list of waypoints for tests and one-off calls.
    /// </summary>
    public List<GridPoint> FindPath(IslandLevel level, GridPoint start, GridPoint goal, out PathResult result, int maxIterations = DEFAULT_MAX_ITERATIONS)
    {
        var waypoints = new List<GridPoint>(32);
        result = FindPath(level, start, goal, waypoints, maxIterations);
        return waypoints;
    }

    private void ResetScratchBuffers()
    {
        _openSet.Clear();
        _gScores.Clear();
        _cameFrom.Clear();
        _closedSet.Clear();
    }

    private PathResult RunAStarSearch(
        IslandLevel level,
        GridPoint start,
        GridPoint goal,
        List<GridPoint> outputBuffer,
        int maxIterations)
    {
        _gScores[start] = 0;
        _openSet.Enqueue(start, ComputeManhattan(start, goal));

        GridPoint closestNode = start;
        int closestDistance = ComputeManhattan(start, goal);
        int iterations = 0;

        while (_openSet.Count > 0 && iterations < maxIterations)
        {
            GridPoint current = _openSet.Dequeue();
            if (!_closedSet.Add(current))
            {
                continue;
            }

            iterations++;
            if (current == goal)
            {
                ReconstructPath(current, outputBuffer);
                return new PathResult(reachedGoal: true, waypointCount: outputBuffer.Count);
            }

            UpdateClosestTracker(current, goal, ref closestNode, ref closestDistance);
            ExpandCardinalNeighbors(level, current, goal);
        }

        return BuildFallbackResult(level, closestNode, goal, outputBuffer);
    }

    private static void UpdateClosestTracker(GridPoint candidate, GridPoint goal, ref GridPoint closestNode, ref int closestDistance)
    {
        int dist = ComputeManhattan(candidate, goal);
        if (dist < closestDistance)
        {
            closestDistance = dist;
            closestNode = candidate;
        }
    }

    private void ExpandCardinalNeighbors(IslandLevel level, GridPoint current, GridPoint goal)
    {
        int tentativeG = _gScores[current] + 1;
        for (int i = 0; i < CARDINAL_OFFSETS.Length; i++)
        {
            var neighbor = new GridPoint(current.Col + CARDINAL_OFFSETS[i].Col, current.Row + CARDINAL_OFFSETS[i].Row);
            if (!level.IsInBounds(neighbor.Col, neighbor.Row) || _closedSet.Contains(neighbor))
            {
                continue;
            }

            if (!level.IsTileWalkable(neighbor.Col, neighbor.Row))
            {
                continue;
            }

            if (!_gScores.TryGetValue(neighbor, out int existingG) || tentativeG < existingG)
            {
                _cameFrom[neighbor] = current;
                _gScores[neighbor] = tentativeG;
                _openSet.Enqueue(neighbor, tentativeG + ComputeManhattan(neighbor, goal));
            }
        }
    }

    private PathResult BuildFallbackResult(IslandLevel level, GridPoint closestNode, GridPoint goal, List<GridPoint> outputBuffer)
    {
        ReconstructPath(closestNode, outputBuffer);
        GridPoint? blockingWall = FindBestAdjacentWall(level, closestNode, goal);
        return new PathResult(reachedGoal: false, waypointCount: outputBuffer.Count, blockingWallTile: blockingWall);
    }

    private static GridPoint? FindBestAdjacentWall(IslandLevel level, GridPoint fromNode, GridPoint goal)
    {
        GridPoint? bestWall = null;
        int bestDist = int.MaxValue;

        for (int i = 0; i < CARDINAL_OFFSETS.Length; i++)
        {
            int col = fromNode.Col + CARDINAL_OFFSETS[i].Col;
            int row = fromNode.Row + CARDINAL_OFFSETS[i].Row;
            if (!level.TryGetTile(col, row, out Tile? tile) || tile is null || !tile.IsOccupiedBySolidStructure)
            {
                continue;
            }

            var candidate = new GridPoint(col, row);
            int dist = ComputeManhattan(candidate, goal);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestWall = candidate;
            }
        }

        return bestWall;
    }

    private void ReconstructPath(GridPoint endNode, List<GridPoint> outputBuffer)
    {
        outputBuffer.Clear();
        GridPoint cursor = endNode;
        outputBuffer.Add(cursor);

        while (_cameFrom.TryGetValue(cursor, out GridPoint previous))
        {
            cursor = previous;
            outputBuffer.Add(cursor);
        }

        outputBuffer.Reverse();
    }
}
