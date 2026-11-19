using System;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.Decorator;

/// <summary>
/// GoF Concrete Decorator: enchants a wrapped <see cref="IWeapon"/> with additional fire damage.
/// </summary>
public sealed class FireDamageDecorator : WeaponUpgradeDecorator
{
    private readonly int _bonusDamage;

    /// <summary>Gets the flat bonus fire damage added to each strike.</summary>
    public int BonusDamage => _bonusDamage;

    /// <inheritdoc />
    public override string Name => $"{InnerWeapon.Name} [Blazing +{_bonusDamage}]";

    /// <summary>
    /// Initializes a new instance of the <see cref="FireDamageDecorator"/> class.
    /// </summary>
    public FireDamageDecorator(IWeapon innerWeapon, int bonusDamage = 8)
        : base(innerWeapon)
    {
        if (bonusDamage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bonusDamage), "Bonus fire damage must be positive.");
        }

        _bonusDamage = bonusDamage;
    }

    /// <inheritdoc />
    public override int ComputeDamage() => InnerWeapon.ComputeDamage() + _bonusDamage;
}
