using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.AI;

/// <summary>
/// GoF Strategy (Concrete Strategy): ranged kiting AI (e.g., Skeleton Archer) that maintains
/// a safe distance band from the player, retreats when pressured, and fires pooled projectiles.
/// <para>
/// Complexity: O(1) per fixed step with zero heap allocations.
/// </para>
/// </summary>
public sealed class RangedKiteStrategy : IAIStrategy
{
    private const float DEFAULT_RETREAT_RATIO = 0.55f;
    private const float DEFAULT_PROJECTILE_SPEED = 240f;
    private const float PATROL_SPEED_FACTOR = 0.45f;

    private readonly EnemyFSM _fsm;
    private readonly CollisionDetector _collisionDetector;
    private readonly Action<Vector2D, Vector2D, int, string>? _projectileSpawner;
    private readonly float _projectileSpeed;
    private readonly float _retreatDistanceRatio;

    /// <inheritdoc />
    public string StrategyName => nameof(RangedKiteStrategy);

    /// <summary>
    /// Initializes a new instance of the <see cref="RangedKiteStrategy"/> class.
    /// </summary>
    public RangedKiteStrategy(
        Action<Vector2D, Vector2D, int, string>? projectileSpawner = null,
        EnemyFSM? fsm = null,
        CollisionDetector? collisionDetector = null,
        float projectileSpeed = DEFAULT_PROJECTILE_SPEED,
        float retreatDistanceRatio = DEFAULT_RETREAT_RATIO)
    {
        if (projectileSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(projectileSpeed), "Projectile speed must be positive.");
        }

        _projectileSpawner = projectileSpawner;
        _fsm = fsm ?? new EnemyFSM(detectionRange: 320f);
        _collisionDetector = collisionDetector ?? new CollisionDetector();
        _projectileSpeed = projectileSpeed;
        _retreatDistanceRatio = global::System.Math.Clamp(retreatDistanceRatio, 0.2f, 0.9f);
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

        Vector2D toPlayer = target.Center - enemy.Center;
        float distance = toPlayer.Length;
        Vector2D aimDirection = toPlayer.Normalized();

        if (distance < enemy.AttackRange * _retreatDistanceRatio)
        {
            MoveWithSliding(enemy, -aimDirection, level, deltaTime);
        }
        else if (distance > enemy.AttackRange)
        {
            MoveWithSliding(enemy, aimDirection, level, deltaTime);
            return;
        }

        TryFireProjectile(enemy, target, aimDirection);
    }

    private void TryFireProjectile(Enemy enemy, Player target, Vector2D aimDirection)
    {
        if (!enemy.CanAttack || aimDirection == Vector2D.Zero)
        {
            return;
        }

        if (_projectileSpawner is not null)
        {
            _projectileSpawner(enemy.Center, aimDirection * _projectileSpeed, enemy.AttackDamage, enemy.Id);
            enemy.TriggerAttackCooldown();
        }
        else
        {
            enemy.TryAttack(target);
        }
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
