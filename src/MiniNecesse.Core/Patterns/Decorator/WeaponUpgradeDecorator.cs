using System;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.Decorator;

/// <summary>
/// GoF Decorator (Abstract Decorator): wraps any <see cref="IWeapon"/> to dynamically extend
/// damage, attack speed, or enchantment titles at runtime without modifying existing weapon classes (OCP).
/// </summary>
public abstract class WeaponUpgradeDecorator : IWeapon
{
    private readonly IWeapon _innerWeapon;

    /// <summary>Gets the wrapped inner weapon instance.</summary>
    protected IWeapon InnerWeapon => _innerWeapon;

    /// <inheritdoc />
    public virtual string WeaponId => _innerWeapon.WeaponId;

    /// <inheritdoc />
    public virtual string Name => _innerWeapon.Name;

    /// <inheritdoc />
    public virtual int BaseDamage => _innerWeapon.BaseDamage;

    /// <inheritdoc />
    public virtual float AttackRange => _innerWeapon.AttackRange;

    /// <inheritdoc />
    public virtual float BaseCooldown => _innerWeapon.BaseCooldown;

    /// <inheritdoc />
    public virtual bool IsRanged => _innerWeapon.IsRanged;

    /// <inheritdoc />
    public virtual float ProjectileSpeed => _innerWeapon.ProjectileSpeed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeaponUpgradeDecorator"/> class.
    /// </summary>
    protected WeaponUpgradeDecorator(IWeapon innerWeapon)
    {
        ArgumentNullException.ThrowIfNull(innerWeapon);
        _innerWeapon = innerWeapon;
    }

    /// <inheritdoc />
    public virtual int ComputeDamage() => _innerWeapon.ComputeDamage();

    /// <inheritdoc />
    public virtual float ComputeCooldown() => _innerWeapon.ComputeCooldown();
}
