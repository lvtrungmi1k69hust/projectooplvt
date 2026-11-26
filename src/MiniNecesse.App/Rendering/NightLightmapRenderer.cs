using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Math;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Renders nighttime ambient darkness and radial player/torch light pools using a low-resolution
/// lightmap grid (16x16 px cells) coalesced into horizontal/vertical spans and drawn via pre-cached
/// <see cref="SolidBrush"/> buckets with zero per-frame allocations and zero GDI+ bitmap rescaling overhead.
/// </summary>
public sealed class NightLightmapRenderer : IDisposable
{
    private const int CELL_SIZE = 16;
    private const int DARK_BUCKETS = 16;
    private const int WARM_BUCKETS = 6;
    private const int MAX_LIGHTS = 32;
    private static readonly float[] FLICKER_OFFSETS = { -4f, 0f, 4f, 2f };

    private struct LightEntry
    {
        public float CellX;
        public float CellY;
        public int QuantX;
        public int QuantY;
        public int QuantRadius;
        public float RadiusSq;
        public float InvRadiusSq;
        public float Intensity;
        public float Warmth;
    }

    private struct LightSpan
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public byte Bucket;
    }

    private readonly SolidBrush[] _darkBrushes = new SolidBrush[DARK_BUCKETS + 1];
    private readonly SolidBrush[] _warmBrushes = new SolidBrush[WARM_BUCKETS + 1];
    private readonly LightEntry[] _lights = new LightEntry[MAX_LIGHTS];

    private byte[] _darkGrid = Array.Empty<byte>();
    private byte[] _warmGrid = Array.Empty<byte>();
    private LightSpan[] _darkSpans = Array.Empty<LightSpan>();
    private LightSpan[] _warmSpans = Array.Empty<LightSpan>();
    private int _darkSpanCount;
    private int _warmSpanCount;

    private long _lastStateHash = long.MinValue;
    private int _lastViewportWidth;
    private int _lastViewportHeight;
    private bool _disposed;

    /// <summary>Initializes pre-allocated darkness and warm-glow brushes.</summary>
    public NightLightmapRenderer()
    {
        for (int i = 0; i <= DARK_BUCKETS; i++)
        {
            int alpha = (int)MathF.Round((i / (float)DARK_BUCKETS) * 185f);
            _darkBrushes[i] = new SolidBrush(Color.FromArgb(alpha, 10, 16, 44));
        }

        for (int i = 0; i <= WARM_BUCKETS; i++)
        {
            int alpha = (int)MathF.Round((i / (float)WARM_BUCKETS) * 48f);
            _warmBrushes[i] = new SolidBrush(Color.FromArgb(alpha, 255, 198, 86));
        }
    }

    /// <summary>Invalidates cached lightmap spans (e.g., when switching islands).</summary>
    public void Invalidate()
    {
        _lastStateHash = long.MinValue;
    }

    /// <summary>
    /// Draws the cached span-merged lightmap across <paramref name="viewport"/> with zero allocations.
    /// Recomputes grid spans only when darkness level, quantized light coordinates, or flicker step change.
    /// </summary>
    public void Render(
        Graphics g,
        Size viewport,
        float darkness,
        Vector2D playerScreenCenter,
        IReadOnlyList<Structure> structures,
        Vector2D cam,
        float animTime)
    {
        if (_disposed || viewport.Width <= 0 || viewport.Height <= 0 || darkness <= 0.02f)
        {
            return;
        }

        int cols = Math.Max(1, (viewport.Width + CELL_SIZE - 1) / CELL_SIZE);
        int rows = Math.Max(1, (viewport.Height + CELL_SIZE - 1) / CELL_SIZE);
        int darknessBucket = Math.Clamp((int)MathF.Round(Math.Clamp(darkness, 0f, 1f) * DARK_BUCKETS), 0, DARK_BUCKETS);
        if (darknessBucket == 0)
        {
            return;
        }

        int flickerBucket = ((int)(animTime * 4f)) & 3;
        float flickerDelta = FLICKER_OFFSETS[flickerBucket];
        int lightCount = CollectLights(viewport, playerScreenCenter, structures, cam, flickerDelta);

        long stateHash = ComputeStateHash(cols, rows, darknessBucket, lightCount);
        if (_lastViewportWidth != viewport.Width ||
            _lastViewportHeight != viewport.Height ||
            _lastStateHash != stateHash)
        {
            EnsureBackingArrays(cols, rows);
            RebuildGridAndSpans(viewport.Width, viewport.Height, cols, rows, darknessBucket, lightCount);
            _lastViewportWidth = viewport.Width;
            _lastViewportHeight = viewport.Height;
            _lastStateHash = stateHash;
        }

        SmoothingMode prevSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None;

        for (int i = 0; i < _darkSpanCount; i++)
        {
            LightSpan span = _darkSpans[i];
            g.FillRectangle(_darkBrushes[span.Bucket], span.X, span.Y, span.Width, span.Height);
        }

        for (int i = 0; i < _warmSpanCount; i++)
        {
            LightSpan span = _warmSpans[i];
            g.FillRectangle(_warmBrushes[span.Bucket], span.X, span.Y, span.Width, span.Height);
        }

        g.SmoothingMode = prevSmoothing;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        for (int i = 0; i < _darkBrushes.Length; i++)
        {
            _darkBrushes[i].Dispose();
        }

        for (int i = 0; i < _warmBrushes.Length; i++)
        {
            _warmBrushes[i].Dispose();
        }

        _darkGrid = Array.Empty<byte>();
        _warmGrid = Array.Empty<byte>();
        _darkSpans = Array.Empty<LightSpan>();
        _warmSpans = Array.Empty<LightSpan>();
        _disposed = true;
    }

    private int CollectLights(
        Size viewport,
        Vector2D playerScreenCenter,
        IReadOnlyList<Structure> structures,
        Vector2D cam,
        float flickerDelta)
    {
        int count = 0;
        AddLightEntry(ref count, playerScreenCenter.X, playerScreenCenter.Y, 114f, intensity: 0.88f, warmth: 0.25f);

        for (int i = 0; i < structures.Count && count < MAX_LIGHTS; i++)
        {
            Structure s = structures[i];
            if (!s.IsAlive || s.LightRadius <= 0f)
            {
                continue;
            }

            float lcx = s.Center.X - cam.X;
            float lcy = s.Center.Y - cam.Y;
            float r = MathF.Max(24f, s.LightRadius + flickerDelta);
            if (lcx + r < 0f || lcx - r > viewport.Width || lcy + r < 0f || lcy - r > viewport.Height)
            {
                continue;
            }

            AddLightEntry(ref count, lcx, lcy, r, intensity: 0.94f, warmth: 0.70f);
        }

        return count;
    }

    private void AddLightEntry(ref int count, float screenX, float screenY, float radiusPx, float intensity, float warmth)
    {
        if (count >= MAX_LIGHTS) return;

        float cellX = (screenX / CELL_SIZE) - 0.5f;
        float cellY = (screenY / CELL_SIZE) - 0.5f;
        float cellRadius = MathF.Max(1.5f, radiusPx / CELL_SIZE);
        float radiusSq = cellRadius * cellRadius;

        _lights[count++] = new LightEntry
        {
            CellX = cellX,
            CellY = cellY,
            QuantX = (int)MathF.Round(screenX / 8f),
            QuantY = (int)MathF.Round(screenY / 8f),
            QuantRadius = (int)MathF.Round(radiusPx / 4f),
            RadiusSq = radiusSq,
            InvRadiusSq = 1f / radiusSq,
            Intensity = intensity,
            Warmth = warmth
        };
    }

    private long ComputeStateHash(int cols, int rows, int darknessBucket, int lightCount)
    {
        unchecked
        {
            long hash = 17L;
            hash = (hash * 31L) + cols;
            hash = (hash * 31L) + rows;
            hash = (hash * 31L) + darknessBucket;
            hash = (hash * 31L) + lightCount;

            for (int i = 0; i < lightCount; i++)
            {
                LightEntry l = _lights[i];
                hash = (hash * 31L) + l.QuantX;
                hash = (hash * 31L) + l.QuantY;
                hash = (hash * 31L) + l.QuantRadius;
            }

            return hash;
        }
    }

    private void EnsureBackingArrays(int cols, int rows)
    {
        int totalCells = cols * rows;
        if (_darkGrid.Length < totalCells)
        {
            _darkGrid = new byte[totalCells];
            _warmGrid = new byte[totalCells];
            _darkSpans = new LightSpan[totalCells];
            _warmSpans = new LightSpan[totalCells];
        }
    }

    private void RebuildGridAndSpans(int viewportW, int viewportH, int cols, int rows, int darknessBucket, int lightCount)
    {
        float normalizedDarkness = darknessBucket / (float)DARK_BUCKETS;

        for (int cy = 0; cy < rows; cy++)
        {
            int rowOffset = cy * cols;
            for (int cx = 0; cx < cols; cx++)
            {
                float remainingDarkness = 1f;
                float warmGlow = 0f;

                for (int i = 0; i < lightCount; i++)
                {
                    LightEntry light = _lights[i];
                    float dx = cx - light.CellX;
                    float dy = cy - light.CellY;
                    float distSq = (dx * dx) + (dy * dy);
                    if (distSq >= light.RadiusSq)
                    {
                        continue;
                    }

                    float falloff = 1f - (distSq * light.InvRadiusSq);
                    float smooth = falloff * falloff;
                    remainingDarkness *= (1f - (smooth * light.Intensity));

                    if (light.Warmth > 0f)
                    {
                        float w = smooth * light.Warmth;
                        if (w > warmGlow)
                        {
                            warmGlow = w;
                        }
                    }
                }

                int darkBucket = Math.Clamp((int)MathF.Round(normalizedDarkness * remainingDarkness * DARK_BUCKETS), 0, DARK_BUCKETS);
                int warmBucket = Math.Clamp((int)MathF.Round(normalizedDarkness * warmGlow * WARM_BUCKETS), 0, WARM_BUCKETS);

                _darkGrid[rowOffset + cx] = (byte)darkBucket;
                _warmGrid[rowOffset + cx] = (byte)warmBucket;
            }
        }

        _darkSpanCount = CoalesceSpans(_darkGrid, viewportW, viewportH, cols, rows, _darkSpans);
        _warmSpanCount = CoalesceSpans(_warmGrid, viewportW, viewportH, cols, rows, _warmSpans);
    }

    private static int CoalesceSpans(byte[] grid, int viewportW, int viewportH, int cols, int rows, LightSpan[] output)
    {
        int spanCount = 0;

        for (int row = 0; row < rows; row++)
        {
            int y = row * CELL_SIZE;
            int h = Math.Min(CELL_SIZE, viewportH - y);
            int rowOffset = row * cols;

            int col = 0;
            while (col < cols)
            {
                byte bucket = grid[rowOffset + col];
                int runStart = col;
                col++;
                while (col < cols && grid[rowOffset + col] == bucket)
                {
                    col++;
                }

                if (bucket == 0)
                {
                    continue;
                }

                int spanX = runStart * CELL_SIZE;
                int spanW = Math.Min((col * CELL_SIZE) - spanX, viewportW - spanX);

                // Merge uniform full-width rows vertically so dark outer sky needs very few Draw calls.
                if (runStart == 0 && col == cols && spanCount > 0)
                {
                    ref LightSpan prev = ref output[spanCount - 1];
                    if (prev.X == 0 && prev.Width == spanW && prev.Bucket == bucket && (prev.Y + prev.Height) == y)
                    {
                        prev.Height += h;
                        continue;
                    }
                }

                output[spanCount++] = new LightSpan
                {
                    X = spanX,
                    Y = y,
                    Width = spanW,
                    Height = h,
                    Bucket = bucket
                };
            }
        }

        return spanCount;
    }
}
