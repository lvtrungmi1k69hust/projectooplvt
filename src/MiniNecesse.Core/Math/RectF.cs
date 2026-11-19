using System;
using System.Globalization;

namespace MiniNecesse.Core.Math;

/// <summary>
/// Represents an immutable 2D axis-aligned bounding box (AABB) defined by its top-left origin and dimensions.
/// Pure C# value type used for collision detection and spatial queries without any UI library dependency.
/// </summary>
public readonly struct RectF : IEquatable<RectF>
{
    private const float EPSILON = 1e-6f;

    /// <summary>Gets an empty rectangle at the origin (0, 0, 0, 0).</summary>
    public static RectF Empty => new(0f, 0f, 0f, 0f);

    /// <summary>Gets the X coordinate of the left edge.</summary>
    public float X { get; }

    /// <summary>Gets the Y coordinate of the top edge.</summary>
    public float Y { get; }

    /// <summary>Gets the non-negative width of the rectangle.</summary>
    public float Width { get; }

    /// <summary>Gets the non-negative height of the rectangle.</summary>
    public float Height { get; }

    /// <summary>Gets the X coordinate of the left edge.</summary>
    public float Left => X;

    /// <summary>Gets the Y coordinate of the top edge.</summary>
    public float Top => Y;

    /// <summary>Gets the X coordinate of the right edge.</summary>
    public float Right => X + Width;

    /// <summary>Gets the Y coordinate of the bottom edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>Gets the top-left position as a <see cref="Vector2D"/>.</summary>
    public Vector2D Position => new(X, Y);

    /// <summary>Gets the width and height as a <see cref="Vector2D"/>.</summary>
    public Vector2D Size => new(Width, Height);

    /// <summary>Gets the center point of the rectangle.</summary>
    public Vector2D Center => new(X + (Width * 0.5f), Y + (Height * 0.5f));

    /// <summary>Gets a value indicating whether the rectangle has zero area.</summary>
    public bool IsEmpty => Width <= EPSILON || Height <= EPSILON;

    /// <summary>
    /// Initializes a new instance of the <see cref="RectF"/> struct.
    /// </summary>
    /// <param name="x">The X coordinate of the top-left corner.</param>
    /// <param name="y">The Y coordinate of the top-left corner.</param>
    /// <param name="width">The width of the rectangle (must be non-negative).</param>
    /// <param name="height">The height of the rectangle (must be non-negative).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="width"/> or <paramref name="height"/> is negative.</exception>
    public RectF(float x, float y, float width, float height)
    {
        if (width < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Rectangle width cannot be negative.");
        }

        if (height < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Rectangle height cannot be negative.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RectF"/> struct from position and size vectors.
    /// </summary>
    public RectF(Vector2D position, Vector2D size)
        : this(position.X, position.Y, size.X, size.Y)
    {
    }

    /// <summary>
    /// Creates a <see cref="RectF"/> centered at <paramref name="center"/> with the specified <paramref name="size"/>.
    /// </summary>
    public static RectF FromCenter(Vector2D center, Vector2D size)
    {
        return new RectF(
            center.X - (size.X * 0.5f),
            center.Y - (size.Y * 0.5f),
            size.X,
            size.Y);
    }

    /// <summary>
    /// Determines whether this rectangle overlaps with <paramref name="other"/> using strict AABB overlap rules.
    /// Complexity: O(1).
    /// </summary>
    public bool Intersects(RectF other)
    {
        if (IsEmpty || other.IsEmpty)
        {
            return false;
        }

        return Left < other.Right
            && Right > other.Left
            && Top < other.Bottom
            && Bottom > other.Top;
    }

    /// <summary>
    /// Determines whether the specified <paramref name="point"/> lies inside or on the boundary of this rectangle.
    /// </summary>
    public bool Contains(Vector2D point)
    {
        return point.X >= Left
            && point.X <= Right
            && point.Y >= Top
            && point.Y <= Bottom;
    }

    /// <summary>
    /// Determines whether the entire <paramref name="other"/> rectangle is contained within this rectangle.
    /// </summary>
    public bool Contains(RectF other)
    {
        return other.Left >= Left
            && other.Right <= Right
            && other.Top >= Top
            && other.Bottom <= Bottom;
    }

    /// <summary>
    /// Computes the overlapping region between this rectangle and <paramref name="other"/>.
    /// Returns <see cref="Empty"/> if the rectangles do not intersect.
    /// </summary>
    public RectF GetIntersection(RectF other)
    {
        float left = MathF.Max(Left, other.Left);
        float top = MathF.Max(Top, other.Top);
        float right = MathF.Min(Right, other.Right);
        float bottom = MathF.Min(Bottom, other.Bottom);

        if (right <= left || bottom <= top)
        {
            return Empty;
        }

        return new RectF(left, top, right - left, bottom - top);
    }

    /// <summary>Returns a new rectangle translated by the specified delta vector.</summary>
    public RectF Offset(Vector2D delta) => new(X + delta.X, Y + delta.Y, Width, Height);

    /// <summary>Returns a new rectangle translated by (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    public RectF Offset(float dx, float dy) => new(X + dx, Y + dy, Width, Height);

    /// <summary>
    /// Returns a new rectangle expanded on both horizontal and vertical sides by the specified amounts.
    /// Resulting dimensions are clamped to zero if deflated past empty.
    /// </summary>
    public RectF Inflate(float horizontal, float vertical)
    {
        float newWidth = MathF.Max(0f, Width + (horizontal * 2f));
        float newHeight = MathF.Max(0f, Height + (vertical * 2f));
        return new RectF(X - horizontal, Y - vertical, newWidth, newHeight);
    }

    /// <summary>Determines whether two rectangles are equal within floating-point tolerance.</summary>
    public static bool operator ==(RectF left, RectF right) => left.Equals(right);

    /// <summary>Determines whether two rectangles are not equal.</summary>
    public static bool operator !=(RectF left, RectF right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(RectF other)
    {
        return MathF.Abs(X - other.X) <= EPSILON
            && MathF.Abs(Y - other.Y) <= EPSILON
            && MathF.Abs(Width - other.Width) <= EPSILON
            && MathF.Abs(Height - other.Height) <= EPSILON;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RectF other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"[X={X:F2}, Y={Y:F2}, W={Width:F2}, H={Height:F2}]");
}
