namespace MiniNecesse.Core.Enums;

/// <summary>
/// Independent world generation mode axis (orthogonal to <see cref="Difficulty"/>).
/// </summary>
public enum GameMode
{
    /// <summary>Hand-crafted progression across 5 themed JSON islands.</summary>
    Campaign = 0,

    /// <summary>Deterministic multi-biome procedural world generated from a numeric seed.</summary>
    Endless = 1
}
