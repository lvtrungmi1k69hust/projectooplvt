using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.AI;

/// <summary>
/// Finite State Machine governing enemy and boss state transitions:
/// Idle -&gt; Patrol -&gt; Chase -&gt; Attack -&gt; Enraged (Boss HP &lt; 30%) -&gt; Dead.
/// <para>
/// Pseudocode:
/// 1. If !enemy.IsAlive -&gt; Dead.
/// 2. If enemy.State == Enraged -&gt; remain Enraged (Boss phase lock).
/// 3. Compute distance to player:
///    a. If distance &lt;= enemy.AttackRange -&gt; Attack.
///    b. Else if distance &lt;= detectionRange -&gt; Chase.
///    c. Else alternate Idle and Patrol using deterministic phase timer.
/// </para>
/// <para>
/// Complexity: O(1) time and O(1) space per tick with zero heap allocations.
/// </para>
/// </summary>
public sealed class EnemyFSM
{
    private const float DEFAULT_DETECTION_RANGE = 260f;
    private const float DEFAULT_IDLE_DURATION = 1.5f;
    private const float DEFAULT_PATROL_DURATION = 2.5f;

    private static readonly Vector2D[] PATROL_DIRECTIONS =
    {
        Vector2D.Right,
        Vector2D.Down,
        Vector2D.Left,
        Vector2D.Up
    };

    private readonly float _detectionRange;
    private readonly float _idleDuration;
    private readonly float _patrolDuration;
    private float _stateTimer;
    private int _patrolDirectionIndex;

    /// <summary>Gets the world-space aggro detection radius.</summary>
    public float DetectionRange => _detectionRange;

    /// <summary>Gets the active patrol heading vector without allocating.</summary>
    public Vector2D CurrentPatrolDirection => PATROL_DIRECTIONS[_patrolDirectionIndex];

    /// <summary>
    /// Initializes a new instance of the <see cref="EnemyFSM"/> class.
    /// </summary>
    public EnemyFSM(
        float detectionRange = DEFAULT_DETECTION_RANGE,
        float idleDuration = DEFAULT_IDLE_DURATION,
        float patrolDuration = DEFAULT_PATROL_DURATION)
    {
        if (detectionRange <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(detectionRange), "Detection range must be positive.");
        }

        if (idleDuration <= 0f || patrolDuration <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(idleDuration), "State durations must be positive.");
        }

        _detectionRange = detectionRange;
        _idleDuration = idleDuration;
        _patrolDuration = patrolDuration;
    }

    /// <summary>
    /// Evaluates transition rules and updates <paramref name="enemy"/>'s <see cref="EnemyState"/>.
    /// Complexity: O(1).
    /// </summary>
    public EnemyState Evaluate(Enemy enemy, Player target, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(target);

        if (!enemy.IsAlive)
        {
            enemy.SetState(EnemyState.Dead);
            return EnemyState.Dead;
        }

        if (enemy.State == EnemyState.Enraged)
        {
            return EnemyState.Enraged;
        }

        if (!target.IsAlive)
        {
            return UpdateAmbientState(enemy, deltaTime);
        }

        float distance = Vector2D.Distance(enemy.Center, target.Center);
        if (distance <= enemy.AttackRange)
        {
            enemy.SetState(EnemyState.Attack);
            return EnemyState.Attack;
        }

        if (distance <= _detectionRange)
        {
            enemy.SetState(EnemyState.Chase);
            return EnemyState.Chase;
        }

        return UpdateAmbientState(enemy, deltaTime);
    }

    private EnemyState UpdateAmbientState(Enemy enemy, float deltaTime)
    {
        _stateTimer += deltaTime;

        if (enemy.State != EnemyState.Idle && enemy.State != EnemyState.Patrol)
        {
            _stateTimer = 0f;
            enemy.SetState(EnemyState.Idle);
            return EnemyState.Idle;
        }

        if (enemy.State == EnemyState.Idle && _stateTimer >= _idleDuration)
        {
            _stateTimer = 0f;
            _patrolDirectionIndex = (_patrolDirectionIndex + 1) % PATROL_DIRECTIONS.Length;
            enemy.SetState(EnemyState.Patrol);
        }
        else if (enemy.State == EnemyState.Patrol && _stateTimer >= _patrolDuration)
        {
            _stateTimer = 0f;
            enemy.SetState(EnemyState.Idle);
        }

        return enemy.State;
    }
}
