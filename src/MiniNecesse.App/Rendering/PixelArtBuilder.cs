using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Builds crisp 16x16 Necesse-style pixel-art <see cref="Bitmap"/> textures once at startup
/// from compact ASCII pixel matrices and color palettes (zero per-frame allocations).
/// </summary>
internal static class PixelArtBuilder
{
    /// <summary>
    /// Creates a transparent 32bpp ARGB <see cref="Bitmap"/> from an ASCII pixel grid.
    /// '.' or ' ' represents transparent pixels.
    /// </summary>
    public static Bitmap FromGrid(string[] rows, IReadOnlyDictionary<char, Color> palette)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);

        for (int y = 0; y < height; y++)
        {
            string line = rows[y];
            for (int x = 0; x < width && x < line.Length; x++)
            {
                char ch = line[x];
                if (ch == '.' || ch == ' ')
                {
                    continue;
                }

                if (palette.TryGetValue(ch, out Color color))
                {
                    bitmap.SetPixel(x, y, color);
                }
            }
        }

        return bitmap;
    }

    /// <summary>Creates a textured 16x16 terrain tile with deterministic pixel-art micro-detail.</summary>
    public static Bitmap CreateTexturedTile(
        Color baseColor,
        Color lightSpeckle,
        Color darkSpeckle,
        int seed,
        int speckleCount = 26,
        Color? accentColor = null,
        int accentCount = 0)
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        var rng = new Random(seed);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bmp.SetPixel(x, y, baseColor);
            }
        }

        for (int i = 0; i < speckleCount; i++)
        {
            int x = rng.Next(0, 16);
            int y = rng.Next(0, 16);
            bmp.SetPixel(x, y, i % 2 == 0 ? lightSpeckle : darkSpeckle);
        }

        if (accentColor.HasValue && accentCount > 0)
        {
            for (int i = 0; i < accentCount; i++)
            {
                int x = rng.Next(1, 15);
                int y = rng.Next(1, 15);
                bmp.SetPixel(x, y, accentColor.Value);
                bmp.SetPixel(x, y - 1, lightSpeckle);
            }
        }

        return bmp;
    }

    /// <summary>Creates a 16x16 animated water tile with crisp pixel-art wave crests.</summary>
    public static Bitmap CreateWaterTile(int frameOffset)
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        Color deep = Color.FromArgb(35, 108, 168);
        Color mid = Color.FromArgb(46, 126, 188);
        Color crest = Color.FromArgb(124, 198, 236);
        Color sparkle = Color.FromArgb(196, 238, 255);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bmp.SetPixel(x, y, (x + y) % 5 == 0 ? mid : deep);
            }
        }

        DrawWaveLine(bmp, (2 + frameOffset) % 16, 4, crest, sparkle);
        DrawWaveLine(bmp, (10 + frameOffset) % 16, 11, crest, sparkle);
        return bmp;
    }

    /// <summary>Creates a 16x16 stone paver or dark fortress brick tile.</summary>
    public static Bitmap CreateStoneBrickTile(Color baseColor, Color mortarColor, Color highlightColor, int seed)
    {
        var bmp = new Bitmap(16, 16, PixelFormat.Format32bppPArgb);
        var rng = new Random(seed);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool isMortar = y == 0 || y == 8 || (y < 8 && x == 0) || (y >= 8 && x == 8);
                bool isHighlight = y == 1 || y == 9;
                if (isMortar)
                {
                    bmp.SetPixel(x, y, mortarColor);
                }
                else if (isHighlight && rng.Next(0, 3) != 0)
                {
                    bmp.SetPixel(x, y, highlightColor);
                }
                else
                {
                    bmp.SetPixel(x, y, rng.Next(0, 7) == 0 ? mortarColor : baseColor);
                }
            }
        }

        return bmp;
    }

    private static void DrawWaveLine(Bitmap bmp, int startX, int y, Color crest, Color sparkle)
    {
        for (int i = 0; i < 5; i++)
        {
            int x = (startX + i) % 16;
            bmp.SetPixel(x, y, i == 2 ? sparkle : crest);
        }
    }
}
