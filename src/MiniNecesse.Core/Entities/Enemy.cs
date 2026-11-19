using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a hostile island creature driven by a runtime-swappable <see cref="IAIStrategy"/>.
/// </summary>
public class Enemy : Character
{
    private readonly string _archetype;
    private readonly int _scoreReward;
    private readonly float _attackRange;
    private EnemyState _state;
    private IAIStrategy? _aiStrategy;
    private int _attackDamage;
    private float _attackCooldown;
    private float _attackCooldownRemaining;

    /// <summary>Gets the enemy archetype name (e.g., Slime, ZombieRaider, SkeletonArcher).</summary>
    public string Archetype => _archetype;

    /// <summary>Gets the score points awarded when this enemy is defeated.</summary>
    public int ScoreReward => _scoreReward;

    /// <summary>Gets the effective attack range in world units.</summary>
    public float AttackRange => _attackRange;

    /// <summary>Gets the damage dealt by each enemy attack.</summary>
    public int AttackDamage => _attackDamage;

    /// <summary>Gets the cooldown between enemy attacks in seconds.</summary>
    public float AttackCooldown => _attackCooldown;

    /// <summary>Gets the current finite state machine state of this enemy.</summary>
    public EnemyState State => _state;

    /// <summary>Gets the currently assigned AI strategy (Association).</summary>
    public IAIStrategy? CurrentStrategy => _aiStrategy;

    /// <summary>Gets a value indicating whether this enemy can attack on this frame.</summary>
    public bool CanAttack => IsAlive && _attackCooldownRemaining <= 0f;

    /// <summary>Gets a value indicating whether this enemy is an island boss.</summary>
    public virtual bool IsBoss => false;

    /// <summary>Raised whenever the enemy's FSM state changes.</summary>
    public event Action<Enemy, EnemyState>? OnStateChanged;

    /// <summary>Raised when the enemy's AI strategy is swapped at runtime.</summary>
    public event Action<Enemy, IAIStrategy>? OnStrategyChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Enemy"/> class.
    /// </summary>
    public Enemy(
        string id,
        string archetype,
        Vector2D spawnPosition,
        int maxHp,
        float moveSpeed,
        int attackDamage,
        float attackRange,
        float attackCooldown,
        int scoreReward = 50,
        IAIStrategy? initialStrategy = null,
        Vector2D? size = null)
        : base(id, spawnPosition, size ?? new Vector2D(24f, 24f), maxHp, moveSpeed)
    {
        if (string.IsNullOrWhiteSpace(archetype))
        {
            throw new ArgumentException("Enemy archetype cannot be empty.", nameof(archetype));
        }

        if (attackDamage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attackDamage), "Attack damage must be positive.");
        }

        if (attackRange <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(attackRange), "Attack range must be positive.");
        }

        if (attackCooldown <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(attackCooldown), "Attack cooldown must be positive.");
        }

        if (scoreReward < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scoreReward), "Score reward cannot be negative.");
        }

        _archetype = archetype;
        _attackDamage = attackDamage;
        _attackRange = attackRange;
        _attackCooldown = attackCooldown;
        _scoreReward = scoreReward;
        _aiStrategy = initialStrategy;
        _state = EnemyState.Idle;
    }

    /// <summary>Replaces the active AI strategy at runtime (Strategy pattern).</summary>
    public void SetStrategy(IAIStrategy newStrategy)
    {
        ArgumentNullException.ThrowIfNull(newStrategy);
        _aiStrategy = newStrategy;
        OnStrategyChanged?.Invoke(this, newStrategy);
    }

    /// <summary>Transitions the enemy to a new FSM state.</summary>
    public void SetState(EnemyState newState)
    {
        if (_state == newState)
        {
            return;
        }

        _state = newState;
        OnStateChanged?.Invoke(this, _state);
    }

    /// <summary>Attempts to attack <paramref name="target"/> if off cooldown.</summary>
    public bool TryAttack(IDamageable target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!CanAttack || !target.IsAlive)
        {
            return false;
        }

        target.TakeDamage(_attackDamage);
        _attackCooldownRemaining = _attackCooldown;
        return true;
    }

    /// <summary>Executes the assigned <see cref="IAIStrategy"/> for one fixed step.</summary>
    public void ExecuteAI(Player target, IslandLevel level, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(level);

        if (!IsAlive || !IsActive)
        {
            return;
        }

        _aiStrategy?.Execute(this, target, level, deltaTime);
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (_attackCooldownRemaining > 0f)
        {
            _attackCooldownRemaining = global::System.Math.Max(0f, _attackCooldownRemaining - deltaTime);
        }
    }

    /// <summary>Adjusts attack stats (used by boss enrage phases or difficulty modifiers).</summary>
    protected void ScaleCombatStats(int newDamage, float newCooldown)
    {
        if (newDamage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newDamage), "Attack damage must be positive.");
        }

        if (newCooldown <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(newCooldown), "Attack cooldown must be positive.");
        }

        _attackDamage = newDamage;
        _attackCooldown = newCooldown;
    }

    /// <inheritdoc />
    protected override void OnHealthDepleted()
    {
        SetState(EnemyState.Dead);
        base.OnHealthDepleted();
    }
}
