using System;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// GoF Decorator over <see cref="IDamageMitigationStrategy"/> that enforces JSON-configured
/// <see cref="DifficultyProfile"/> incoming-damage rules (e.g., 0 damage in Tutorial or 1.3x in Hard)
/// cleanly via OCP without scattered <c>if (tutorial)</c> checks across entities.
/// </summary>
public sealed class DifficultyDamageMitigationDecorator : IDamageMitigationStrategy
{
    private readonly IDamageMitigationStrategy _innerStrategy;
    private readonly DifficultyProfile _profile;

    /// <summary>Gets the active difficulty profile driving mitigation scaling.</summary>
    public DifficultyProfile Profile => _profile;

    /// <summary>Initializes a new instance of the <see cref="DifficultyDamageMitigationDecorator"/> class.</summary>
    public DifficultyDamageMitigationDecorator(IDamageMitigationStrategy innerStrategy, DifficultyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(innerStrategy);
        ArgumentNullException.ThrowIfNull(profile);

        _innerStrategy = innerStrategy;
        _profile = profile;
    }

    /// <inheritdoc />
    public int ComputeFinalDamage(int rawDamage, int totalDefense)
    {
        if (rawDamage <= 0 || _profile.DamageTakenMultiplier <= 0f || _profile.PreventPlayerDeath)
        {
            return 0;
        }

        int baseMitigated = _innerStrategy.ComputeFinalDamage(rawDamage, totalDefense);
        if (baseMitigated <= 0)
        {
            return 0;
        }

        return global::System.Math.Max(1, (int)MathF.Round(baseMitigated * _profile.DamageTakenMultiplier));
    }

    /// <inheritdoc />
    public int ComputeDirectDamage(int rawDamage)
    {
        if (rawDamage <= 0 || _profile.DamageTakenMultiplier <= 0f || _profile.PreventPlayerDeath)
        {
            return 0;
        }

        int innerDirect = _innerStrategy.ComputeDirectDamage(rawDamage);
        if (innerDirect <= 0)
        {
            return 0;
        }

        return global::System.Math.Max(1, (int)MathF.Round(innerDirect * _profile.DamageTakenMultiplier));
    }
}
