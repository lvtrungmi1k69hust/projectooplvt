using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// O(1), zero-allocation Finite State Machine governing friendly settler behavior:
/// Idle &lt;-&gt; Patrol / Work -&gt; ReturnHome (Night) -&gt; Flee (Low HP near enemy).
/// </summary>
public sealed class SettlerFSM
{
    private const float THREAT_RADIUS = 96f;
    private const float DEFAULT_IDLE_DURATION = 1.6f;
    private const float DEFAULT_PATROL_DURATION = 2.4f;

    private static readonly Vector2D[] PATROL_DIRS =
    {
        Vector2D.Right,
        Vector2D.Down,
        Vector2D.Left,
        Vector2D.Up
    };

    private float _stateTimer;
    private int _patrolIndex;

    /// <summary>Gets the active patrol vector without allocations.</summary>
    public Vector2D CurrentPatrolDirection => PATROL_DIRS[_patrolIndex];

    /// <summary>Evaluates next <see cref="SettlerState"/> and returns the nearest threat enemy if within threat range.</summary>
    public SettlerState Evaluate(
        Settler settler,
        IReadOnlyList<Enemy> enemies,
        IslandLevel level,
        DayPhase phase,
        float deltaTime,
        out Enemy? nearestThreat)
    {
        ArgumentNullException.ThrowIfNull(settler);
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(level);

        nearestThreat = FindNearestLivingEnemy(settler.Center, enemies, THREAT_RADIUS);
        if (nearestThreat is not null && settler.HealthRatio < 0.50f)
        {
            settler.SetState(SettlerState.Flee);
            return SettlerState.Flee;
        }

        if (phase == DayPhase.Night && settler.HomeTile.HasValue)
        {
            GridPoint home = settler.HomeTile.Value;
            Vector2D homeWorld = level.TileToWorldCenter(home.Col, home.Row);
            if (Vector2D.DistanceSquared(settler.Center, homeWorld) > 28f * 28f)
            {
                settler.SetState(SettlerState.ReturnHome);
                return SettlerState.ReturnHome;
            }

            settler.SetState(SettlerState.Idle);
            return SettlerState.Idle;
        }

        if (phase == DayPhase.Day)
        {
            settler.SetState(SettlerState.Work);
            return SettlerState.Work;
        }

        return UpdateAmbientPatrol(settler, deltaTime);
    }

    private SettlerState UpdateAmbientPatrol(Settler settler, float deltaTime)
    {
        _stateTimer += deltaTime;
        if (settler.State != SettlerState.Idle && settler.State != SettlerState.Patrol)
        {
            _stateTimer = 0f;
            settler.SetState(SettlerState.Idle);
            return SettlerState.Idle;
        }

        if (settler.State == SettlerState.Idle && _stateTimer >= DEFAULT_IDLE_DURATION)
        {
            _stateTimer = 0f;
            _patrolIndex = (_patrolIndex + 1) % PATROL_DIRS.Length;
            settler.SetState(SettlerState.Patrol);
        }
        else if (settler.State == SettlerState.Patrol && _stateTimer >= DEFAULT_PATROL_DURATION)
        {
            _stateTimer = 0f;
            settler.SetState(SettlerState.Idle);
        }

        return settler.State;
    }

    private static Enemy? FindNearestLivingEnemy(Vector2D origin, IReadOnlyList<Enemy> enemies, float maxRadius)
    {
        Enemy? best = null;
        float bestDistSq = maxRadius * maxRadius;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (!e.IsAlive)
            {
                continue;
            }

            float distSq = Vector2D.DistanceSquared(origin, e.Center);
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                best = e;
            }
        }

        return best;
    }
}
