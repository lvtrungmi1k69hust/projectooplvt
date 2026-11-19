using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Contract for entities capable of continuous 2D movement in world space.
/// </summary>
public interface IMovable
{
    /// <summary>Gets the current world position.</summary>
    Vector2D Position { get; }

    /// <summary>Gets the current velocity vector in world units per second.</summary>
    Vector2D Velocity { get; }

    /// <summary>Gets the positive base movement speed in world units per second.</summary>
    float MoveSpeed { get; }

    /// <summary>Gets the discrete 8-way direction the entity is facing.</summary>
    Direction FacingDirection { get; }

    /// <summary>Moves the entity along the normalized <paramref name="direction"/> over <paramref name="deltaTime"/> seconds.</summary>
    void Move(Vector2D direction, float deltaTime);

    /// <summary>Repositions the entity directly to <paramref name="position"/> (e.g., collision resolution or teleport).</summary>
    void SetPosition(Vector2D position);
}
