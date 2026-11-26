using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.AI;

/// <summary>
/// GoF Strategy (Concrete Strategy): aggressive boss enrage behavior dynamically swapped via
/// <see cref="Enemy.SetAIStrategy(IAIStrategy)"/> when Boss HP drops below 30%.
/// Charges the player relentlessly while firing radial spread projectile volleys from an object pool.
/// <para>
/// Complexity: O(P) per attack volley where P is volley projectile count (3-5); zero heap allocations per tick.
/// </para>
/// </summary>
public sealed class BossEnrageStrategy : IAIStrategy
{
    private const float DEFAULT_VOLLEY_SPREAD_RADIANS = 0.65f;
    private const float DEFAULT_PROJECTILE_SPEED = 260f;
    private const float DEFAULT_MAX_VOLLEY_RANGE = 420f;
    private const int DEFAULT_VOLLEY_COUNT = 3;

    private readonly CollisionDetector _collisionDetector;
    private readonly Action<Vector2D, Vector2D, int, string>? _projectileSpawner;
    private readonly int _volleyCount;
    private readonly float _projectileSpeed;
    private readonly float _spreadRadians;
    private readonly float _maxVolleyRange;

    /// <inheritdoc />
    public string StrategyName => nameof(BossEnrageStrategy);

    /// <summary>Gets the number of projectiles fired in each radial burst.</summary>
    public int VolleyCount => _volleyCount;

    /// <summary>Gets the maximum distance in world units at which enraged projectile volleys fire.</summary>
    public float MaxVolleyRange => _maxVolleyRange;

    /// <summary>
    /// Initializes a new instance of the <see cref="BossEnrageStrategy"/> class.
    /// </summary>
    public BossEnrageStrategy(
        Action<Vector2D, Vector2D, int, string>? projectileSpawner = null,
        CollisionDetector? collisionDetector = null,
        int volleyCount = DEFAULT_VOLLEY_COUNT,
        float projectileSpeed = DEFAULT_PROJECTILE_SPEED,
        float spreadRadians = DEFAULT_VOLLEY_SPREAD_RADIANS,
        float maxVolleyRange = DEFAULT_MAX_VOLLEY_RANGE)
    {
        if (volleyCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volleyCount), "Volley count must be positive.");
        }

        if (projectileSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(projectileSpeed), "Projectile speed must be positive.");
        }

        if (maxVolleyRange <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxVolleyRange), "Max volley range must be positive.");
        }

        _projectileSpawner = projectileSpawner;
        _collisionDetector = collisionDetector ?? new CollisionDetector();
        _volleyCount = volleyCount;
        _projectileSpeed = projectileSpeed;
        _spreadRadians = spreadRadians;
        _maxVolleyRange = maxVolleyRange;
    }

    /// <inheritdoc />
    public void Execute(Enemy enemy, Player target, IslandLevel level, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(level);

        if (!enemy.IsAlive || !target.IsAlive)
        {
            return;
        }

        Vector2D toPlayer = target.Center - enemy.Center;
        float distance = toPlayer.Length;
        Vector2D direction = toPlayer.Normalized();

        if (direction != Vector2D.Zero && distance > enemy.AttackRange * 0.6f)
        {
            Vector2D delta = direction * (enemy.MoveSpeed * deltaTime);
            _collisionDetector.MoveWithTileCollision(enemy, delta, enemy.Size, level);
        }

        if (!enemy.CanAttack)
        {
            return;
        }

        if (distance <= enemy.AttackRange)
        {
            enemy.TryAttack(target);
            return;
        }

        if (_projectileSpawner is not null && direction != Vector2D.Zero && distance <= _maxVolleyRange)
        {
            SpawnVolley(enemy, direction);
            enemy.TriggerAttackCooldown();
        }
    }

    private void SpawnVolley(Enemy enemy, Vector2D centerDirection)
    {
        if (_projectileSpawner is null || centerDirection == Vector2D.Zero)
        {
            return;
        }

        float baseAngle = MathF.Atan2(centerDirection.Y, centerDirection.X);
        float startOffset = _volleyCount == 1 ? 0f : -(_spreadRadians * 0.5f);
        float step = _volleyCount == 1 ? 0f : _spreadRadians / (_volleyCount - 1);

        for (int i = 0; i < _volleyCount; i++)
        {
            float angle = baseAngle + startOffset + (step * i);
            var velocity = new Vector2D(MathF.Cos(angle), MathF.Sin(angle)) * _projectileSpeed;
            _projectileSpawner(enemy.Center, velocity, enemy.AttackDamage, enemy.Id);
        }
    }
}
