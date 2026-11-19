using System;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Transient collision descriptor passed into <see cref="RuleEngine.ResolveCollision(CollisionInfo)"/>
/// without being stored as a field (demonstrates UML Dependency).
/// </summary>
public readonly struct CollisionInfo
{
    /// <summary>Gets the identifier of the first colliding entity.</summary>
    public string EntityAId { get; }

    /// <summary>Gets the identifier of the second colliding entity.</summary>
    public string EntityBId { get; }

    /// <summary>Gets the overlapping AABB region of the collision.</summary>
    public RectF OverlapRegion { get; }

    /// <summary>Gets the unit normal pointing from entity B toward entity A.</summary>
    public Vector2D SeparationNormal { get; }

    /// <summary>Gets the non-negative penetration depth along <see cref="SeparationNormal"/>.</summary>
    public float PenetrationDepth { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CollisionInfo"/> struct.
    /// </summary>
    public CollisionInfo(
        string entityAId,
        string entityBId,
        RectF overlapRegion,
        Vector2D separationNormal,
        float penetrationDepth)
    {
        if (string.IsNullOrWhiteSpace(entityAId) || string.IsNullOrWhiteSpace(entityBId))
        {
            throw new ArgumentException("Colliding entity ids cannot be empty.");
        }

        if (penetrationDepth < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(penetrationDepth), "Penetration depth cannot be negative.");
        }

        EntityAId = entityAId;
        EntityBId = entityBId;
        OverlapRegion = overlapRegion;
        SeparationNormal = separationNormal.Normalized();
        PenetrationDepth = penetrationDepth;
    }
}
