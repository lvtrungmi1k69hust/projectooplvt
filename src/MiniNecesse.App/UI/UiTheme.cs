using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MiniNecesse.App.UI;

/// <summary>
/// Centralized color palette, typography, spacing, and corner-radius tokens for MiniNecesse UI
/// alongside reusable GDI+ brushes/pens/fonts to ensure zero per-frame allocations in Paint handlers.
/// </summary>
public static class UiTheme
{
    public const string FontFamily = "Segoe UI";
    public const int CardRadius = 12;
    public const int ControlRadius = 8;
    public const int PillRadius = 6;
    public const int PagePadding = 22;
    public const int SectionGap = 14;

    public static readonly Color BackgroundPrimary = Color.FromArgb(18, 24, 34);
    public static readonly Color SurfaceCard = Color.FromArgb(24, 32, 46);
    public static readonly Color SurfaceHeader = Color.FromArgb(20, 27, 39);
    public static readonly Color SurfaceInput = Color.FromArgb(30, 39, 54);
    public static readonly Color RowEven = Color.FromArgb(26, 35, 50);
    public static readonly Color RowOdd = Color.FromArgb(31, 41, 58);
    public static readonly Color RowHover = Color.FromArgb(40, 54, 78);
    public static readonly Color RowHighlight = Color.FromArgb(56, 48, 28);
    public static readonly Color RowHighlightHover = Color.FromArgb(68, 58, 34);

    public static readonly Color BorderSubtle = Color.FromArgb(48, 62, 86);
    public static readonly Color AccentGold = Color.FromArgb(240, 184, 77);
    public static readonly Color AccentGoldDark = Color.FromArgb(172, 126, 44);
    public static readonly Color AccentGreen = Color.FromArgb(46, 156, 98);
    public static readonly Color AccentBlue = Color.FromArgb(48, 118, 190);
    public static readonly Color ButtonNeutral = Color.FromArgb(64, 78, 102);
    public static readonly Color ButtonDanger = Color.FromArgb(164, 58, 58);

    public static readonly Color TextPrimary = Color.FromArgb(238, 242, 248);
    public static readonly Color TextSecondary = Color.FromArgb(196, 208, 224);
    public static readonly Color TextMuted = Color.FromArgb(142, 158, 182);
    public static readonly Color TextDarkOnAccent = Color.FromArgb(18, 22, 30);

    public static readonly Color MedalGold = Color.FromArgb(246, 196, 69);
    public static readonly Color MedalGoldRim = Color.FromArgb(188, 132, 24);
    public static readonly Color MedalSilver = Color.FromArgb(207, 216, 227);
    public static readonly Color MedalSilverRim = Color.FromArgb(136, 148, 166);
    public static readonly Color MedalBronze = Color.FromArgb(209, 137, 78);
    public static readonly Color MedalBronzeRim = Color.FromArgb(146, 86, 40);
    public static readonly Color RibbonRed = Color.FromArgb(192, 62, 74);

    /// <summary>Creates a rounded-rectangle <see cref="GraphicsPath"/> during layout/resize (never inside OnPaint).</summary>
    public static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Pre-allocated GDI+ brush, pen, font, and string-format set for owner-drawn <see cref="MiniNecesse.App"/> UI controls.
/// Disposed deterministically by each owning control.
/// </summary>
public sealed class UiThemeResources : IDisposable
{
    private bool _disposed;

    public SolidBrush BackgroundBrush { get; } = new(UiTheme.BackgroundPrimary);
    public SolidBrush CardBrush { get; } = new(UiTheme.SurfaceCard);
    public SolidBrush HeaderBandBrush { get; } = new(UiTheme.SurfaceHeader);
    public SolidBrush InputBrush { get; } = new(UiTheme.SurfaceInput);
    public SolidBrush RowEvenBrush { get; } = new(UiTheme.RowEven);
    public SolidBrush RowOddBrush { get; } = new(UiTheme.RowOdd);
    public SolidBrush RowHoverBrush { get; } = new(UiTheme.RowHover);
    public SolidBrush RowHighlightBrush { get; } = new(UiTheme.RowHighlight);
    public SolidBrush RowHighlightHoverBrush { get; } = new(UiTheme.RowHighlightHover);
    public SolidBrush AccentGoldBrush { get; } = new(UiTheme.AccentGold);
    public SolidBrush TextPrimaryBrush { get; } = new(UiTheme.TextPrimary);
    public SolidBrush TextSecondaryBrush { get; } = new(UiTheme.TextSecondary);
    public SolidBrush TextMutedBrush { get; } = new(UiTheme.TextMuted);
    public SolidBrush TextDarkBrush { get; } = new(UiTheme.TextDarkOnAccent);

    public SolidBrush MedalGoldBrush { get; } = new(UiTheme.MedalGold);
    public SolidBrush MedalSilverBrush { get; } = new(UiTheme.MedalSilver);
    public SolidBrush MedalBronzeBrush { get; } = new(UiTheme.MedalBronze);
    public SolidBrush RibbonBrush { get; } = new(UiTheme.RibbonRed);
    public SolidBrush HighlightSheenBrush { get; } = new(Color.FromArgb(95, 255, 255, 255));

    public Pen BorderSubtlePen { get; } = new(UiTheme.BorderSubtle, 1.25f);
    public Pen AccentGoldPen { get; } = new(UiTheme.AccentGold, 1.5f);
    public Pen MedalGoldRimPen { get; } = new(UiTheme.MedalGoldRim, 1.75f);
    public Pen MedalSilverRimPen { get; } = new(UiTheme.MedalSilverRim, 1.75f);
    public Pen MedalBronzeRimPen { get; } = new(UiTheme.MedalBronzeRim, 1.75f);
    public Pen TrophyOutlinePen { get; } = new(UiTheme.AccentGoldDark, 2.5f);

    public Font HeaderFont { get; } = new(UiTheme.FontFamily, 9f, FontStyle.Bold);
    public Font RowPrimaryFont { get; } = new(UiTheme.FontFamily, 10.5f, FontStyle.Bold);
    public Font RowBodyFont { get; } = new(UiTheme.FontFamily, 10f, FontStyle.Regular);
    public Font MedalNumberFont { get; } = new(UiTheme.FontFamily, 8.5f, FontStyle.Bold);
    public Font BadgeFont { get; } = new(UiTheme.FontFamily, 7.25f, FontStyle.Bold);
    public Font EmptyTitleFont { get; } = new(UiTheme.FontFamily, 13f, FontStyle.Bold);

    public StringFormat LeftMiddleFormat { get; } = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    public StringFormat CenterMiddleFormat { get; } = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    public StringFormat RightMiddleFormat { get; } = new()
    {
        Alignment = StringAlignment.Far,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        BackgroundBrush.Dispose();
        CardBrush.Dispose();
        HeaderBandBrush.Dispose();
        InputBrush.Dispose();
        RowEvenBrush.Dispose();
        RowOddBrush.Dispose();
        RowHoverBrush.Dispose();
        RowHighlightBrush.Dispose();
        RowHighlightHoverBrush.Dispose();
        AccentGoldBrush.Dispose();
        TextPrimaryBrush.Dispose();
        TextSecondaryBrush.Dispose();
        TextMutedBrush.Dispose();
        TextDarkBrush.Dispose();
        MedalGoldBrush.Dispose();
        MedalSilverBrush.Dispose();
        MedalBronzeBrush.Dispose();
        RibbonBrush.Dispose();
        HighlightSheenBrush.Dispose();

        BorderSubtlePen.Dispose();
        AccentGoldPen.Dispose();
        MedalGoldRimPen.Dispose();
        MedalSilverRimPen.Dispose();
        MedalBronzeRimPen.Dispose();
        TrophyOutlinePen.Dispose();

        HeaderFont.Dispose();
        RowPrimaryFont.Dispose();
        RowBodyFont.Dispose();
        MedalNumberFont.Dispose();
        BadgeFont.Dispose();
        EmptyTitleFont.Dispose();

        LeftMiddleFormat.Dispose();
        CenterMiddleFormat.Dispose();
        RightMiddleFormat.Dispose();
    }
}
