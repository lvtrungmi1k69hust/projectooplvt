using System;
using System.Globalization;

namespace MiniNecesse.Core.Math;

/// <summary>
/// Represents an immutable 2D floating-point vector in world or grid space.
/// Pure C# value type with zero dependency on UI or game engine libraries.
/// </summary>
public readonly struct Vector2D : IEquatable<Vector2D>
{
    private const float EPSILON = 1e-6f;

    /// <summary>Gets the horizontal component of the vector.</summary>
    public float X { get; }

    /// <summary>Gets the vertical component of the vector.</summary>
    public float Y { get; }

    /// <summary>Gets the zero vector (0, 0).</summary>
    public static Vector2D Zero => new(0f, 0f);

    /// <summary>Gets the vector (1, 1).</summary>
    public static Vector2D One => new(1f, 1f);

    /// <summary>Gets the unit vector along the positive X axis (1, 0).</summary>
    public static Vector2D UnitX => new(1f, 0f);

    /// <summary>Gets the unit vector along the positive Y axis (0, 1).</summary>
    public static Vector2D UnitY => new(0f, 1f);

    /// <summary>Gets the upward direction vector in top-down screen/world coordinates (0, -1).</summary>
    public static Vector2D Up => new(0f, -1f);

    /// <summary>Gets the downward direction vector in top-down screen/world coordinates (0, 1).</summary>
    public static Vector2D Down => new(0f, 1f);

    /// <summary>Gets the leftward direction vector (-1, 0).</summary>
    public static Vector2D Left => new(-1f, 0f);

    /// <summary>Gets the rightward direction vector (1, 0).</summary>
    public static Vector2D Right => new(1f, 0f);

    /// <summary>
    /// Initializes a new instance of the <see cref="Vector2D"/> struct.
    /// </summary>
    /// <param name="x">The X coordinate.</param>
    /// <param name="y">The Y coordinate.</param>
    public Vector2D(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Gets the squared Euclidean length of the vector.</summary>
    public float LengthSquared => (X * X) + (Y * Y);

    /// <summary>Gets the Euclidean length (magnitude) of the vector.</summary>
    public float Length => MathF.Sqrt(LengthSquared);

    /// <summary>
    /// Returns a unit-length vector pointing in the same direction, or <see cref="Zero"/>
    /// if the vector's length is effectively zero.
    /// </summary>
    public Vector2D Normalized()
    {
        float length = Length;
        if (length <= EPSILON)
        {
            return Zero;
        }

        return new Vector2D(X / length, Y / length);
    }

    /// <summary>Calculates the Euclidean distance between two points.</summary>
    public static float Distance(Vector2D a, Vector2D b)
    {
        return (a - b).Length;
    }

    /// <summary>Calculates the squared Euclidean distance between two points.</summary>
    public static float DistanceSquared(Vector2D a, Vector2D b)
    {
        return (a - b).LengthSquared;
    }

    /// <summary>Calculates the dot product of two vectors.</summary>
    public static float Dot(Vector2D a, Vector2D b)
    {
        return (a.X * b.X) + (a.Y * b.Y);
    }

    /// <summary>Linearly interpolates between two vectors by factor <paramref name="t"/> clamped to [0, 1].</summary>
    public static Vector2D Lerp(Vector2D start, Vector2D end, float t)
    {
        float clampedT = global::System.Math.Clamp(t, 0f, 1f);
        return new Vector2D(
            start.X + ((end.X - start.X) * clampedT),
            start.Y + ((end.Y - start.Y) * clampedT));
    }

    /// <summary>Clamps each component of <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>.</summary>
    public static Vector2D Clamp(Vector2D value, Vector2D min, Vector2D max)
    {
        return new Vector2D(
            global::System.Math.Clamp(value.X, min.X, max.X),
            global::System.Math.Clamp(value.Y, min.Y, max.Y));
    }

    /// <summary>Adds two vectors component-wise.</summary>
    public static Vector2D operator +(Vector2D left, Vector2D right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>Subtracts the second vector from the first component-wise.</summary>
    public static Vector2D operator -(Vector2D left, Vector2D right) => new(left.X - right.X, left.Y - right.Y);

    /// <summary>Negates a vector.</summary>
    public static Vector2D operator -(Vector2D value) => new(-value.X, -value.Y);

    /// <summary>Multiplies a vector by a scalar.</summary>
    public static Vector2D operator *(Vector2D vector, float scalar) => new(vector.X * scalar, vector.Y * scalar);

    /// <summary>Multiplies a scalar by a vector.</summary>
    public static Vector2D operator *(float scalar, Vector2D vector) => new(vector.X * scalar, vector.Y * scalar);

    /// <summary>Divides a vector by a non-zero scalar.</summary>
    /// <exception cref="DivideByZeroException">Thrown when <paramref name="scalar"/> is zero.</exception>
    public static Vector2D operator /(Vector2D vector, float scalar)
    {
        if (MathF.Abs(scalar) <= EPSILON)
        {
            throw new DivideByZeroException("Cannot divide a Vector2D by zero.");
        }

        return new Vector2D(vector.X / scalar, vector.Y / scalar);
    }

    /// <summary>Determines whether two vectors are equal within floating-point tolerance.</summary>
    public static bool operator ==(Vector2D left, Vector2D right) => left.Equals(right);

    /// <summary>Determines whether two vectors are not equal.</summary>
    public static bool operator !=(Vector2D left, Vector2D right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(Vector2D other)
    {
        return MathF.Abs(X - other.X) <= EPSILON && MathF.Abs(Y - other.Y) <= EPSILON;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Vector2D other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"({X:F2}, {Y:F2})");
}
