using System;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Abstract base class for all spatial objects existing in the 2D top-down game world.
/// </summary>
public abstract class Entity
{
    private readonly string _id;
    private Vector2D _position;
    private Vector2D _size;
    private bool _isActive;

    /// <summary>Gets the unique identifier of this entity.</summary>
    public string Id => _id;

    /// <summary>Gets the top-left world position of this entity.</summary>
    public Vector2D Position => _position;

    /// <summary>Gets the bounding box dimensions (width, height) in world units.</summary>
    public Vector2D Size => _size;

    /// <summary>Gets a value indicating whether this entity participates in simulation updates.</summary>
    public bool IsActive => _isActive;

    /// <summary>Gets the center point of this entity's bounding box.</summary>
    public Vector2D Center => GetBounds().Center;

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity"/> class.
    /// </summary>
    /// <param name="id">Unique non-empty entity identifier.</param>
    /// <param name="position">Initial top-left world coordinates.</param>
    /// <param name="size">Positive width and height in world units.</param>
    protected Entity(string id, Vector2D position, Vector2D size)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Entity id cannot be null or whitespace.", nameof(id));
        }

        if (size.X <= 0f || size.Y <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Entity size must have positive width and height.");
        }

        _id = id;
        _position = position;
        _size = size;
        _isActive = true;
    }

    /// <summary>Returns the current axis-aligned bounding box of this entity.</summary>
    public RectF GetBounds() => new(_position, _size);

    /// <summary>Deactivates this entity so it is excluded from active gameplay updates.</summary>
    public void Deactivate() => _isActive = false;

    /// <summary>Updates entity state for one fixed simulation step.</summary>
    /// <param name="deltaTime">Elapsed fixed time step in seconds.</param>
    public abstract void Update(float deltaTime);

    /// <summary>Updates the entity's internal world position.</summary>
    protected void UpdatePosition(Vector2D newPosition) => _position = newPosition;

    /// <summary>Reactivates a pooled or deactivated entity.</summary>
    protected void Activate() => _isActive = true;

    /// <summary>Updates the entity's bounding dimensions.</summary>
    protected void UpdateSize(Vector2D newSize)
    {
        if (newSize.X <= 0f || newSize.Y <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(newSize), "Entity size must be positive.");
        }

        _size = newSize;
    }
}
