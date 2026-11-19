using System;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Patterns.Flyweight;

/// <summary>
/// GoF Flyweight (Intrinsic Shared State): stores immutable static properties shared across
/// all map <see cref="MiniNecesse.Core.Entities.Tile"/> instances of the same <see cref="TileType"/>.
/// Eliminates redundant memory duplication across thousands of tile objects.
/// </summary>
public sealed class TileDataFlyweight
{
    /// <summary>Gets the terrain archetype represented by this flyweight.</summary>
    public TileType TileType { get; }

    /// <summary>Gets the surface hardness value (0 for liquid, higher for rock/fortress).</summary>
    public int Hardness { get; }

    /// <summary>Gets a value indicating whether ground entities can walk on this terrain.</summary>
    public bool IsWalkable { get; }

    /// <summary>Gets the movement speed multiplier applied on this terrain.</summary>
    public float MovementSpeedMultiplier { get; }

    /// <summary>Gets the platform-agnostic hexadecimal base color code (e.g., "#4CAF50").</summary>
    public string ColorHex { get; }

    /// <summary>Gets the logical sprite asset identifier used by rendering layers.</summary>
    public string SpriteId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TileDataFlyweight"/> class.
    /// </summary>
    public TileDataFlyweight(
        TileType tileType,
        int hardness,
        bool isWalkable,
        float movementSpeedMultiplier,
        string colorHex,
        string spriteId)
    {
        if (hardness < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hardness), "Hardness cannot be negative.");
        }

        if (movementSpeedMultiplier < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(movementSpeedMultiplier), "Speed multiplier cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(colorHex))
        {
            throw new ArgumentException("ColorHex cannot be empty.", nameof(colorHex));
        }

        if (string.IsNullOrWhiteSpace(spriteId))
        {
            throw new ArgumentException("SpriteId cannot be empty.", nameof(spriteId));
        }

        TileType = tileType;
        Hardness = hardness;
        IsWalkable = isWalkable;
        MovementSpeedMultiplier = movementSpeedMultiplier;
        ColorHex = colorHex;
        SpriteId = spriteId;
    }
}
