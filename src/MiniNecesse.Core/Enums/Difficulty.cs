namespace MiniNecesse.Core.Enums;

/// <summary>
/// Defines the gameplay difficulty presets scaling enemy strength and wave intensity.
/// </summary>
public enum Difficulty
{
    /// <summary>Reduced enemy damage and slower raid progression.</summary>
    Easy = 0,

    /// <summary>Standard balanced survival experience.</summary>
    Normal = 1,

    /// <summary>Increased enemy lethality and aggressive AI scaling.</summary>
    Hard = 2
}
