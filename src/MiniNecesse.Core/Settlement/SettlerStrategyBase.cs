using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Shared base class for settler occupation strategies providing zero-allocation pathfinding,
/// FSM evaluation, movement collision sliding, and flee/return-home navigation (DRY).
/// </summary>
public abstract class SettlerStrategyBase : ISettlerStrategy
{
    private const float REPATH_INTERVAL = 0.55f;

    private readonly Func<string, Item> _itemResolver;
    private readonly PathfindingCoordinator _pathCoordinator;
    private readonly CollisionDetector _collisionDetector;
    private readonly SettlerFSM _fsm = new();
    private readonly List<GridPoint> _waypointBuffer = new(48);

    private float _pathCooldown;
    private int _waypointIndex;
    private GridPoint? _lastGoal;
    private int _lastStructureVersion = -1;

    protected IReadOnlyList<Enemy> ContextEnemies { get; private set; } = Array.Empty<Enemy>();
    protected IReadOnlyList<ResourceNode> ContextResources { get; private set; } = Array.Empty<ResourceNode>();
    protected DayPhase ContextPhase { get; private set; } = DayPhase.Day;
    protected Action<Vector2D, Vector2D, int, string>? ContextProjectileSpawner { get; private set; }
    protected Func<string, Item> ItemResolver => _itemResolver;

    /// <inheritdoc />
    public abstract string StrategyName { get; }

    /// <inheritdoc />
    public abstract SettlerProfession Profession { get; }

    /// <summary>Initializes shared settler AI dependencies.</summary>
    protected SettlerStrategyBase(
        Func<string, Item> itemResolver,
        PathfindingCoordinator pathCoordinator,
        CollisionDetector collisionDetector,
        float initialStaggerSeconds = 0f)
    {
        ArgumentNullException.ThrowIfNull(itemResolver);
        ArgumentNullException.ThrowIfNull(pathCoordinator);
        ArgumentNullException.ThrowIfNull(collisionDetector);

        _itemResolver = itemResolver;
        _pathCoordinator = pathCoordinator;
        _collisionDetector = collisionDetector;
        _pathCooldown = MathF.Max(0f, initialStaggerSeconds);
    }

    /// <inheritdoc />
    public void SetWorldContext(
        IReadOnlyList<Enemy> enemies,
        IReadOnlyList<ResourceNode> resources,
        DayPhase phase,
        Action<Vector2D, Vector2D, int, string>? projectileSpawner)
    {
        ContextEnemies = enemies ?? Array.Empty<Enemy>();
        ContextResources = resources ?? Array.Empty<ResourceNode>();
        ContextPhase = phase;
        ContextProjectileSpawner = projectileSpawner;
    }

    /// <inheritdoc />
    public void Execute(Settler settler, Player player, IslandLevel level, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(settler);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(level);

        SettlerState state = _fsm.Evaluate(settler, ContextEnemies, level, ContextPhase, deltaTime, out Enemy? threat);
        switch (state)
        {
            case SettlerState.Flee when threat is not null:
                Vector2D away = (settler.Center - threat.Center).Normalized();
                MoveWithSliding(settler, away, level, deltaTime);
                return;

            case SettlerState.ReturnHome when settler.HomeTile.HasValue:
                Vector2D homeWorld = level.TileToWorldCenter(settler.HomeTile.Value.Col, settler.HomeTile.Value.Row);
                NavigateToward(settler, homeWorld, level, deltaTime);
                return;

            case SettlerState.Patrol:
                MoveWithSliding(settler, _fsm.CurrentPatrolDirection * 0.55f, level, deltaTime);
                return;

            case SettlerState.Work:
                ExecuteWork(settler, player, level, deltaTime);
                return;
        }
    }

    /// <summary>Executes occupation-specific behavior while in <see cref="SettlerState.Work"/>.</summary>
    protected abstract void ExecuteWork(Settler settler, Player player, IslandLevel level, float deltaTime);

    /// <summary>Navigates <paramref name="settler"/> toward <paramref name="goalWorld"/> using shared A* coordinator.</summary>
    protected void NavigateToward(Settler settler, Vector2D goalWorld, IslandLevel level, float deltaTime)
    {
        _pathCooldown -= deltaTime;
        (int startCol, int startRow) = level.WorldToTile(settler.Center);
        (int goalCol, int goalRow) = level.WorldToTile(goalWorld);
        var start = new GridPoint(startCol, startRow);
        var goal = new GridPoint(goalCol, goalRow);

        if (_pathCooldown <= 0f || _waypointBuffer.Count == 0 || _lastGoal != goal || _lastStructureVersion != level.StructureVersion)
        {
            if (_pathCoordinator.TryRequestPath(level, start, goal, _waypointBuffer, out _, out _))
            {
                _pathCooldown = REPATH_INTERVAL;
                _waypointIndex = _waypointBuffer.Count > 1 ? 1 : 0;
                _lastGoal = goal;
                _lastStructureVersion = level.StructureVersion;
            }
            else
            {
                _pathCooldown = 0.08f;
            }
        }

        Vector2D dir;
        if (_waypointBuffer.Count > 0 && _waypointIndex < _waypointBuffer.Count)
        {
            GridPoint wp = _waypointBuffer[_waypointIndex];
            Vector2D wpWorld = level.TileToWorldCenter(wp.Col, wp.Row);
            if (Vector2D.Distance(settler.Center, wpWorld) <= level.TileSize * 0.35f &&
                _waypointIndex < _waypointBuffer.Count - 1)
            {
                _waypointIndex++;
                wp = _waypointBuffer[_waypointIndex];
                wpWorld = level.TileToWorldCenter(wp.Col, wp.Row);
            }

            dir = (wpWorld - settler.Center).Normalized();
        }
        else
        {
            dir = (goalWorld - settler.Center).Normalized();
        }

        MoveWithSliding(settler, dir, level, deltaTime);
    }

    /// <summary>Moves <paramref name="settler"/> cleanly using AABB tile collision sliding.</summary>
    protected void MoveWithSliding(Settler settler, Vector2D direction, IslandLevel level, float deltaTime)
    {
        if (direction == Vector2D.Zero || deltaTime <= 0f)
        {
            return;
        }

        Vector2D delta = direction.Normalized() * (settler.MoveSpeed * deltaTime);
        _collisionDetector.MoveWithTileCollision(settler, delta, settler.Size, level);
    }
}
