using System;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Contract for entities that possess health, can take damage, and can be healed.
/// Implementations must uphold the invariant 0 &lt;= CurrentHp &lt;= MaxHp.
/// </summary>
public interface IDamageable
{
    /// <summary>Gets the entity's current health points.</summary>
    int CurrentHp { get; }

    /// <summary>Gets the entity's maximum health points.</summary>
    int MaxHp { get; }

    /// <summary>Gets a value indicating whether the entity still has positive health.</summary>
    bool IsAlive { get; }

    /// <summary>Raised whenever health changes, reporting (currentHp, maxHp).</summary>
    event Action<int, int>? OnHealthChanged;

    /// <summary>Raised when health drops to zero.</summary>
    event Action<IDamageable>? OnDefeated;

    /// <summary>Applies non-negative damage to the entity, clamping health at zero.</summary>
    /// <param name="amount">Positive damage amount to deduct.</param>
    void TakeDamage(int amount);

    /// <summary>Restores health up to <see cref="MaxHp"/>.</summary>
    /// <param name="amount">Positive healing amount to restore.</param>
    void Heal(int amount);
}
