using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Tests.Math;

public sealed class GeometryPrimitivesTests
{
    [Fact]
    public void Vector2D_Normalized_ReturnsUnitVectorForNonZeroInput()
    {
        var vector = new Vector2D(3f, 4f);

        Vector2D normalized = vector.Normalized();

        Assert.Equal(1f, normalized.Length, precision: 4);
        Assert.Equal(0.6f, normalized.X, precision: 4);
        Assert.Equal(0.8f, normalized.Y, precision: 4);
    }

    [Fact]
    public void Vector2D_Normalized_ReturnsZeroForZeroVector()
    {
        Vector2D normalized = Vector2D.Zero.Normalized();

        Assert.Equal(Vector2D.Zero, normalized);
    }

    [Fact]
    public void RectF_Intersects_ReturnsTrueWhenOverlappingAndFalseWhenSeparated()
    {
        var a = new RectF(0f, 0f, 32f, 32f);
        var overlapping = new RectF(16f, 16f, 32f, 32f);
        var adjacent = new RectF(32f, 0f, 32f, 32f);

        Assert.True(a.Intersects(overlapping));
        Assert.False(a.Intersects(adjacent));
    }

    [Fact]
    public void RectF_GetIntersection_ComputesOverlapRegion()
    {
        var a = new RectF(0f, 0f, 20f, 20f);
        var b = new RectF(10f, 5f, 20f, 20f);

        RectF overlap = a.GetIntersection(b);

        Assert.Equal(new RectF(10f, 5f, 10f, 15f), overlap);
    }

    [Fact]
    public void RectF_NegativeDimensions_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectF(0f, 0f, -1f, 10f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectF(0f, 0f, 10f, -5f));
    }
}
