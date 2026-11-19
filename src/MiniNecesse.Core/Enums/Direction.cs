namespace MiniNecesse.Core.Enums;

/// <summary>
/// Represents 8-way cardinal and diagonal movement or facing directions.
/// </summary>
public enum Direction
{
    /// <summary>No movement direction.</summary>
    None = 0,

    /// <summary>North (negative Y).</summary>
    Up = 1,

    /// <summary>South (positive Y).</summary>
    Down = 2,

    /// <summary>West (negative X).</summary>
    Left = 3,

    /// <summary>East (positive X).</summary>
    Right = 4,

    /// <summary>North-West.</summary>
    UpLeft = 5,

    /// <summary>North-East.</summary>
    UpRight = 6,

    /// <summary>South-West.</summary>
    DownLeft = 7,

    /// <summary>South-East.</summary>
    DownRight = 8
}
