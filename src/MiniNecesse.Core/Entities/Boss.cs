using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents an island boss inheriting from <see cref="Enemy"/> that automatically
/// transitions into an enraged phase and swaps AI strategy below the critical health threshold.
/// </summary>
public sealed class Boss : Enemy
{
    private const float DEFAULT_ENRAGE_THRESHOLD = 0.30f;
    private const float ENRAGE_SPEED_MULTIPLIER = 1.15f;
    private const float ENRAGE_COOLDOWN_MULTIPLIER = 0.70f;
    private const float ENRAGE_DAMAGE_MULTIPLIER = 1.25f;

    private readonly string _bossName;
    private readonly float _enrageThresholdRatio;
    private bool _isEnraged;
    private IAIStrategy? _enrageStrategy;

    /// <summary>Gets the display name of this island boss.</summary>
    public string BossName => _bossName;

    /// <summary>Gets a value indicating whether the boss has entered its enraged phase.</summary>
    public bool IsEnraged => _isEnraged;

    /// <summary>Gets the health ratio below which the boss becomes enraged.</summary>
    public float EnrageThresholdRatio => _enrageThresholdRatio;

    /// <inheritdoc />
    public override bool IsBoss => true;

    /// <summary>Raised once when the boss enters its enraged phase.</summary>
    public event Action<Boss>? OnEnraged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Boss"/> class.
    /// </summary>
    public Boss(
        string id,
        string bossName,
        Vector2D spawnPosition,
        int maxHp,
        float moveSpeed,
        int attackDamage,
        float attackRange,
        float attackCooldown,
        int scoreReward = 500,
        IAIStrategy? initialStrategy = null,
        IAIStrategy? enrageStrategy = null,
        float enrageThresholdRatio = DEFAULT_ENRAGE_THRESHOLD,
        string? archetype = null)
        : base(
            id,
            string.IsNullOrWhiteSpace(archetype) ? bossName : archetype,
            spawnPosition,
            maxHp,
            moveSpeed,
            attackDamage,
            attackRange,
            attackCooldown,
            scoreReward,
            initialStrategy,
            new Vector2D(44f, 44f))
    {
        if (enrageThresholdRatio <= 0f || enrageThresholdRatio >= 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(enrageThresholdRatio),
                "Enrage threshold ratio must be strictly between 0 and 1.");
        }

        _bossName = bossName;
        _enrageThresholdRatio = enrageThresholdRatio;
        _enrageStrategy = enrageStrategy;
    }

    /// <summary>Assigns or replaces the AI strategy activated when the boss enrages.</summary>
    public void SetEnrageStrategy(IAIStrategy enrageStrategy)
    {
        ArgumentNullException.ThrowIfNull(enrageStrategy);
        _enrageStrategy = enrageStrategy;
    }

    /// <inheritdoc />
    public override void TakeDamage(int amount)
    {
        base.TakeDamage(amount);

        if (IsAlive && !_isEnraged && HealthRatio < _enrageThresholdRatio)
        {
            EnterEnragedPhase();
        }
    }

    private void EnterEnragedPhase()
    {
        _isEnraged = true;
        SetState(EnemyState.Enraged);
        SetMoveSpeed(MoveSpeed * ENRAGE_SPEED_MULTIPLIER);

        int boostedDamage = global::System.Math.Max(AttackDamage + 1, (int)MathF.Ceiling(AttackDamage * ENRAGE_DAMAGE_MULTIPLIER));
        float reducedCooldown = global::System.Math.Max(0.25f, AttackCooldown * ENRAGE_COOLDOWN_MULTIPLIER);
        ScaleCombatStats(boostedDamage, reducedCooldown);

        if (_enrageStrategy is not null)
        {
            SetAIStrategy(_enrageStrategy);
        }

        OnEnraged?.Invoke(this);
    }
}
