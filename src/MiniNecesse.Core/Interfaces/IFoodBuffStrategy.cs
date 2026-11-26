namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Strategy pattern contract describing a temporary buff granted after consuming food (OCP).
/// </summary>
public interface IFoodBuffStrategy
{
    /// <summary>Gets the unique identifier of this buff (used for refresh/stack replacement).</summary>
    string BuffId { get; }

    /// <summary>Gets the player-facing display name shown on the HUD.</summary>
    string DisplayName { get; }

    /// <summary>Gets total duration of the buff in seconds.</summary>
    float DurationSeconds { get; }

    /// <summary>Gets the movement speed multiplier (>= 1.0 gives a speed boost).</summary>
    float SpeedMultiplier { get; }

    /// <summary>Gets flat bonus attack damage applied to melee and ranged weapons.</summary>
    int BonusDamage { get; }

    /// <summary>Gets flat bonus defense applied to damage mitigation.</summary>
    int BonusDefense { get; }

    /// <summary>Gets passive health regeneration per second while active.</summary>
    float RegenPerSecond { get; }
}
