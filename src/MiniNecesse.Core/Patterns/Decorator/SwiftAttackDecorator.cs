using System;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.Decorator;

/// <summary>
/// GoF Concrete Decorator: reduces a wrapped <see cref="IWeapon"/>'s attack cooldown for faster strikes.
/// </summary>
public sealed class SwiftAttackDecorator : WeaponUpgradeDecorator
{
    private const float MIN_COOLDOWN_SECONDS = 0.08f;

    private readonly float _cooldownMultiplier;

    /// <summary>Gets the multiplicative cooldown factor strictly within (0, 1).</summary>
    public float CooldownMultiplier => _cooldownMultiplier;

    /// <inheritdoc />
    public override string Name => $"{InnerWeapon.Name} [Swift]";

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftAttackDecorator"/> class.
    /// </summary>
    public SwiftAttackDecorator(IWeapon innerWeapon, float cooldownMultiplier = 0.80f)
        : base(innerWeapon)
    {
        if (cooldownMultiplier <= 0f || cooldownMultiplier >= 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cooldownMultiplier),
                "Cooldown multiplier must be strictly between 0 and 1.");
        }

        _cooldownMultiplier = cooldownMultiplier;
    }

    /// <inheritdoc />
    public override float ComputeCooldown()
    {
        return MathF.Max(MIN_COOLDOWN_SECONDS, InnerWeapon.ComputeCooldown() * _cooldownMultiplier);
    }
}
