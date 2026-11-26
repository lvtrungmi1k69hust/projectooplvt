using System;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.Decorator;

/// <summary>
/// Concrete GoF Decorator wrapping an <see cref="IWeapon"/> with temporary bonus attack damage
/// granted by active food buffs without mutating the underlying weapon item.
/// </summary>
public sealed class FoodDamageBuffDecorator : WeaponUpgradeDecorator
{
    private readonly int _bonusDamage;

    /// <summary>Gets bonus attack damage granted by the active food buff.</summary>
    public int BonusDamage => _bonusDamage;

    /// <inheritdoc />
    public override string Name => $"{InnerWeapon.Name} (Fed +{_bonusDamage})";

    /// <summary>Initializes a new instance of the <see cref="FoodDamageBuffDecorator"/> class.</summary>
    public FoodDamageBuffDecorator(IWeapon innerWeapon, int bonusDamage)
        : base(innerWeapon)
    {
        if (bonusDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bonusDamage), "Food bonus damage cannot be negative.");
        }

        _bonusDamage = bonusDamage;
    }

    /// <inheritdoc />
    public override int ComputeDamage() => base.ComputeDamage() + _bonusDamage;
}
