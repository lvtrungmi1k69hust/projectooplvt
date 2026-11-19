namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Contract for melee and ranged weapons, designed to support runtime Decorator upgrades.
/// </summary>
public interface IWeapon
{
    /// <summary>Gets the unique identifier of this weapon.</summary>
    string WeaponId { get; }

    /// <summary>Gets the display name of this weapon.</summary>
    string Name { get; }

    /// <summary>Gets the base damage dealt per hit.</summary>
    int BaseDamage { get; }

    /// <summary>Gets the effective attack reach in world units.</summary>
    float AttackRange { get; }

    /// <summary>Gets the base cooldown between consecutive attacks in seconds.</summary>
    float BaseCooldown { get; }

    /// <summary>Gets a value indicating whether this weapon fires projectiles.</summary>
    bool IsRanged { get; }

    /// <summary>Gets the projectile launch speed in world units per second (0 for melee).</summary>
    float ProjectileSpeed { get; }

    /// <summary>Computes the effective damage including any applied decorators or modifiers.</summary>
    int ComputeDamage();

    /// <summary>Computes the effective attack cooldown in seconds including any modifiers.</summary>
    float ComputeCooldown();
}
