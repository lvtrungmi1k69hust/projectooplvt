using System;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a moving ranged attack projectile fired by the player, enemies, or bosses.
/// Supports in-place reset for zero-allocation object pooling.
/// </summary>
public sealed class Projectile : Entity
{
    private Vector2D _velocity;
    private int _damage;
    private float _maxLifetime;
    private float _elapsedLifetime;
    private string _ownerId;
    private bool _isFromPlayer;

    /// <summary>Gets the projectile's linear velocity in world units per second.</summary>
    public Vector2D Velocity => _velocity;

    /// <summary>Gets the damage dealt upon hitting a valid target.</summary>
    public int Damage => _damage;

    /// <summary>Gets the maximum lifetime in seconds before the projectile expires.</summary>
    public float MaxLifetime => _maxLifetime;

    /// <summary>Gets the elapsed lifetime in seconds.</summary>
    public float ElapsedLifetime => _elapsedLifetime;

    /// <summary>Gets the identifier of the entity that fired this projectile.</summary>
    public string OwnerId => _ownerId;

    /// <summary>Gets a value indicating whether this projectile was fired by the player.</summary>
    public bool IsFromPlayer => _isFromPlayer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Projectile"/> class.
    /// </summary>
    public Projectile(
        string id,
        Vector2D origin,
        Vector2D velocity,
        int damage,
        float maxLifetime,
        string ownerId,
        bool isFromPlayer,
        Vector2D? size = null)
        : base(id, origin, size ?? new Vector2D(8f, 8f))
    {
        _ownerId = string.Empty;
        Configure(origin, velocity, damage, maxLifetime, ownerId, isFromPlayer);
    }

    /// <summary>Reinitializes this projectile for reuse from an object pool.</summary>
    public void Reset(
        Vector2D origin,
        Vector2D velocity,
        int damage,
        float maxLifetime,
        string ownerId,
        bool isFromPlayer)
    {
        Configure(origin, velocity, damage, maxLifetime, ownerId, isFromPlayer);
        Activate();
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (!IsActive)
        {
            return;
        }

        UpdatePosition(Position + (_velocity * deltaTime));
        _elapsedLifetime += deltaTime;

        if (_elapsedLifetime >= _maxLifetime)
        {
            Deactivate();
        }
    }

    private void Configure(
        Vector2D origin,
        Vector2D velocity,
        int damage,
        float maxLifetime,
        string ownerId,
        bool isFromPlayer)
    {
        if (damage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), "Projectile damage must be positive.");
        }

        if (maxLifetime <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLifetime), "Projectile lifetime must be positive.");
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Projectile ownerId cannot be empty.", nameof(ownerId));
        }

        UpdatePosition(origin);
        _velocity = velocity;
        _damage = damage;
        _maxLifetime = maxLifetime;
        _elapsedLifetime = 0f;
        _ownerId = ownerId;
        _isFromPlayer = isFromPlayer;
    }
}
