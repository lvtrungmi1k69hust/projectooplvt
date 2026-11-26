using System;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Immutable data-driven implementation of <see cref="IFoodBuffStrategy"/> loaded from <c>foods.json</c>.
/// </summary>
public sealed class StatFoodBuffStrategy : IFoodBuffStrategy
{
    /// <inheritdoc />
    public string BuffId { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public float DurationSeconds { get; }

    /// <inheritdoc />
    public float SpeedMultiplier { get; }

    /// <inheritdoc />
    public int BonusDamage { get; }

    /// <inheritdoc />
    public int BonusDefense { get; }

    /// <inheritdoc />
    public float RegenPerSecond { get; }

    /// <summary>Initializes a new instance of the <see cref="StatFoodBuffStrategy"/> class.</summary>
    public StatFoodBuffStrategy(
        string buffId,
        string displayName,
        float durationSeconds,
        float speedMultiplier = 1f,
        int bonusDamage = 0,
        int bonusDefense = 0,
        float regenPerSecond = 0f)
    {
        if (string.IsNullOrWhiteSpace(buffId))
        {
            throw new ArgumentException("BuffId cannot be empty.", nameof(buffId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));
        }

        if (durationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Buff duration must be positive.");
        }

        if (speedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "Speed multiplier must be positive.");
        }

        if (bonusDamage < 0 || bonusDefense < 0 || regenPerSecond < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(bonusDamage), "Bonus buff stats cannot be negative.");
        }

        BuffId = buffId;
        DisplayName = displayName;
        DurationSeconds = durationSeconds;
        SpeedMultiplier = speedMultiplier;
        BonusDamage = bonusDamage;
        BonusDefense = bonusDefense;
        RegenPerSecond = regenPerSecond;
    }
}
