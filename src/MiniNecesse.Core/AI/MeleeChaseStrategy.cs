using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.AI;

/// <summary>
/// GoF Strategy (Concrete Strategy): pursues the player along an A* shortest path and attacks in melee range.
/// When the player walls themselves in, automatically redirects to breach the nearest blocking wall structure.
/// <para>
/// Complexity: Recomputes A* path at a bounded cadence (O((V+E) log V) amortized) using reusable waypoint buffers (0 GC allocs per tick).
/// </para>
/// </summary>
public sealed class MeleeChaseStrategy : IAIStrategy
{
    private const float PATH_RECOMPUTE_INTERVAL = 0.35f;
    private const float PATROL_SPEED_FACTOR = 0.45f;

    private readonly AStarPathfinder _pathfinder;
    private readonly EnemyFSM _fsm;
    private readonly CollisionDetector _collisionDetector;
    private readonly Func<GridPoint, Structure?>? _structureAtTileLookup;
    private readonly List<GridPoint> _waypointBuffer = new(64);

    private float _pathCooldown;
    private int _currentWaypointIndex;
    private GridPoint? _blockingWallTile;

    /// <inheritdoc />
    public string StrategyName => nameof(MeleeChaseStrategy);

    /// <summary>
    /// Initializes a new instance of the <see cref="MeleeChaseStrategy"/> class.
    /// </summary>
    public MeleeChaseStrategy(
        AStarPathfinder? pathfinder = null,
        EnemyFSM? fsm = null,
        CollisionDetector? collisionDetector = null,
        Func<GridPoint, Structure?>? structureAtTileLookup = null)
    {
        _pathfinder = pathfinder ?? new AStarPathfinder();
        _fsm = fsm ?? new EnemyFSM();
        _collisionDetector = collisionDetector ?? new CollisionDetector();
        _structureAtTileLookup = structureAtTileLookup;
    }

    /// <inheritdoc />
    public void Execute(Enemy enemy, Player target, IslandLevel level, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(level);

        EnemyState state = _fsm.Evaluate(enemy, target, deltaTime);
        if (state == EnemyState.Dead || state == EnemyState.Idle)
        {
            return;
        }

        if (state == EnemyState.Patrol)
        {
            MoveWithSliding(enemy, _fsm.CurrentPatrolDirection * PATROL_SPEED_FACTOR, level, deltaTime);
            return;
        }

        if (state == EnemyState.Attack)
        {
            enemy.TryAttack(target);
            return;
        }

        ExecuteChaseOrBreach(enemy, target, level, deltaTime);
    }

    private void ExecuteChaseOrBreach(Enemy enemy, Player target, IslandLevel level, float deltaTime)
    {
        RefreshPathIfDue(enemy, target, level, deltaTime);

        if (_blockingWallTile.HasValue && TryBreachBlockingWall(enemy, level))
        {
            return;
        }

        Vector2D moveDirection = ComputeNextMoveDirection(enemy, target, level);
        MoveWithSliding(enemy, moveDirection, level, deltaTime);
    }

    private void RefreshPathIfDue(Enemy enemy, Player target, IslandLevel level, float deltaTime)
    {
        _pathCooldown -= deltaTime;
        if (_pathCooldown > 0f && _waypointBuffer.Count > 0)
        {
            return;
        }

        _pathCooldown = PATH_RECOMPUTE_INTERVAL;
        (int startCol, int startRow) = level.WorldToTile(enemy.Center);
        (int goalCol, int goalRow) = level.WorldToTile(target.Center);

        PathResult result = _pathfinder.FindPath(
            level,
            new GridPoint(startCol, startRow),
            new GridPoint(goalCol, goalRow),
            _waypointBuffer);

        _currentWaypointIndex = _waypointBuffer.Count > 1 ? 1 : 0;
        _blockingWallTile = result.BlockingWallTile;
    }

    private bool TryBreachBlockingWall(Enemy enemy, IslandLevel level)
    {
        if (!_blockingWallTile.HasValue || _structureAtTileLookup is null)
        {
            return false;
        }

        Structure? wall = _structureAtTileLookup(_blockingWallTile.Value);
        if (wall is null || !wall.IsAlive)
        {
            _blockingWallTile = null;
            return false;
        }

        Vector2D wallCenter = level.TileToWorldCenter(_blockingWallTile.Value.Col, _blockingWallTile.Value.Row);
        if (Vector2D.Distance(enemy.Center, wallCenter) <= enemy.AttackRange + (level.TileSize * 0.5f))
        {
            enemy.TryAttack(wall);
            return true;
        }

        return false;
    }

    private Vector2D ComputeNextMoveDirection(Enemy enemy, Player target, IslandLevel level)
    {
        if (_waypointBuffer.Count == 0 || _currentWaypointIndex >= _waypointBuffer.Count)
        {
            return (target.Center - enemy.Center).Normalized();
        }

        GridPoint waypoint = _waypointBuffer[_currentWaypointIndex];
        Vector2D waypointWorld = level.TileToWorldCenter(waypoint.Col, waypoint.Row);

        if (Vector2D.Distance(enemy.Center, waypointWorld) <= level.TileSize * 0.35f &&
            _currentWaypointIndex < _waypointBuffer.Count - 1)
        {
            _currentWaypointIndex++;
            waypoint = _waypointBuffer[_currentWaypointIndex];
            waypointWorld = level.TileToWorldCenter(waypoint.Col, waypoint.Row);
        }

        return (waypointWorld - enemy.Center).Normalized();
    }

    private void MoveWithSliding(Enemy enemy, Vector2D direction, IslandLevel level, float deltaTime)
    {
        if (direction == Vector2D.Zero)
        {
            return;
        }

        Vector2D delta = direction.Normalized() * (enemy.MoveSpeed * deltaTime);
        _collisionDetector.MoveWithTileCollision(enemy, delta, enemy.Size, level);
    }
}
