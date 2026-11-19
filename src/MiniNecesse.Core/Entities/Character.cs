using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Abstract base class for living, movable world actors such as players, enemies, and bosses.
/// Enforces health invariants (0 &lt;= CurrentHp &lt;= MaxHp) and positive movement speed.
/// </summary>
public abstract class Character : Entity, IDamageable, IMovable
{
    private const float DIRECTION_EPSILON = 0.01f;

    private int _currentHp;
    private readonly int _maxHp;
    private float _moveSpeed;
    private Vector2D _velocity;
    private Direction _facingDirection;

    /// <inheritdoc />
    public int CurrentHp => _currentHp;

    /// <inheritdoc />
    public int MaxHp => _maxHp;

    /// <inheritdoc />
    public bool IsAlive => _currentHp > 0;

    /// <summary>Gets the normalized ratio of remaining health in [0, 1].</summary>
    public float HealthRatio => (float)_currentHp / _maxHp;

    /// <inheritdoc />
    public Vector2D Velocity => _velocity;

    /// <inheritdoc />
    public float MoveSpeed => _moveSpeed;

    /// <inheritdoc />
    public Direction FacingDirection => _facingDirection;

    /// <inheritdoc />
    public event Action<int, int>? OnHealthChanged;

    /// <inheritdoc />
    public event Action<IDamageable>? OnDefeated;

    /// <summary>
    /// Initializes a new instance of the <see cref="Character"/> class.
    /// </summary>
    protected Character(string id, Vector2D position, Vector2D size, int maxHp, float moveSpeed)
        : base(id, position, size)
    {
        if (maxHp <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Maximum health must be greater than zero.");
        }

        if (moveSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(moveSpeed), "Movement speed must be greater than zero.");
        }

        _maxHp = maxHp;
        _currentHp = maxHp;
        _moveSpeed = moveSpeed;
        _velocity = Vector2D.Zero;
        _facingDirection = Direction.Down;
    }

    /// <inheritdoc />
    public virtual void TakeDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Damage amount cannot be negative.");
        }

        if (amount == 0 || !IsAlive)
        {
            return;
        }

        _currentHp = global::System.Math.Max(0, _currentHp - amount);
        OnHealthChanged?.Invoke(_currentHp, _maxHp);

        if (_currentHp == 0)
        {
            OnHealthDepleted();
            OnDefeated?.Invoke(this);
        }
    }

    /// <inheritdoc />
    public virtual void Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Heal amount cannot be negative.");
        }

        if (amount == 0 || !IsAlive || _currentHp == _maxHp)
        {
            return;
        }

        _currentHp = global::System.Math.Min(_maxHp, _currentHp + amount);
        OnHealthChanged?.Invoke(_currentHp, _maxHp);
    }

    /// <inheritdoc />
    public void Move(Vector2D direction, float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (!IsAlive || !IsActive)
        {
            _velocity = Vector2D.Zero;
            return;
        }

        Vector2D normalized = direction.Normalized();
        _velocity = normalized * _moveSpeed;
        if (normalized != Vector2D.Zero)
        {
            _facingDirection = ResolveDirection(normalized);
            UpdatePosition(Position + (_velocity * deltaTime));
        }
    }

    /// <inheritdoc />
    public void SetPosition(Vector2D position) => UpdatePosition(position);

    /// <summary>Restores exact health within [0, MaxHp] during save-state deserialization.</summary>
    public void RestoreHealth(int hp)
    {
        if (hp < 0 || hp > _maxHp)
        {
            throw new ArgumentOutOfRangeException(nameof(hp), "Restored HP must be within [0, MaxHp].");
        }

        _currentHp = hp;
        OnHealthChanged?.Invoke(_currentHp, _maxHp);
    }

    /// <summary>Updates the character's positive movement speed (e.g., boss enrage or buffs).</summary>
    protected void SetMoveSpeed(float newSpeed)
    {
        if (newSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(newSpeed), "Movement speed must remain positive.");
        }

        _moveSpeed = newSpeed;
    }

    /// <summary>Hook invoked immediately when health drops to zero before <see cref="OnDefeated"/>.</summary>
    protected virtual void OnHealthDepleted()
    {
        _velocity = Vector2D.Zero;
        Deactivate();
    }

    private static Direction ResolveDirection(Vector2D normalized)
    {
        bool left = normalized.X < -DIRECTION_EPSILON;
        bool right = normalized.X > DIRECTION_EPSILON;
        bool up = normalized.Y < -DIRECTION_EPSILON;
        bool down = normalized.Y > DIRECTION_EPSILON;

        if (up && left) return Direction.UpLeft;
        if (up && right) return Direction.UpRight;
        if (down && left) return Direction.DownLeft;
        if (down && right) return Direction.DownRight;
        if (up) return Direction.Up;
        if (down) return Direction.Down;
        if (left) return Direction.Left;
        return right ? Direction.Right : Direction.None;
    }
}
