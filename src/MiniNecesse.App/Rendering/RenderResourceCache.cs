using System;
using System.Collections.Generic;
using System.Drawing;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Pre-allocated GDI+ resource cache for <see cref="TopDownRenderer"/>.
/// Reuses <see cref="SolidBrush"/>, <see cref="Pen"/>, and <see cref="Font"/> instances across frames
/// to eliminate GC allocations inside WinForms <c>OnPaint</c>.
/// </summary>
public sealed class RenderResourceCache : IDisposable
{
    private const int NIGHT_ALPHA_BUCKETS = 16;

    private readonly Dictionary<string, SolidBrush> _hexBrushes = new(StringComparer.OrdinalIgnoreCase);
    private readonly SolidBrush[] _nightOverlayBrushes = new SolidBrush[NIGHT_ALPHA_BUCKETS + 1];
    private bool _disposed;

    public SolidBrush ShadowBrush { get; } = new(Color.FromArgb(85, 0, 0, 0));
    public SolidBrush HudPanelBrush { get; } = new(Color.FromArgb(196, 16, 22, 34));
    public SolidBrush HpBackgroundBrush { get; } = new(Color.FromArgb(65, 22, 28));
    public SolidBrush HpFillBrush { get; } = new(Color.FromArgb(224, 68, 76));
    public SolidBrush BossHpFillBrush { get; } = new(Color.FromArgb(198, 42, 92));
    public SolidBrush EnemyHpFillBrush { get; } = new(Color.FromArgb(236, 98, 62));
    public SolidBrush ResourceHpFillBrush { get; } = new(Color.FromArgb(230, 186, 68));
    public SolidBrush HotbarSlotBrush { get; } = new(Color.FromArgb(205, 24, 32, 48));
    public SolidBrush HotbarSelectedBrush { get; } = new(Color.FromArgb(230, 56, 76, 108));
    public SolidBrush TreeTrunkBrush { get; } = new(Color.FromArgb(116, 74, 42));
    public SolidBrush TreeLeafDarkBrush { get; } = new(Color.FromArgb(36, 118, 52));
    public SolidBrush TreeLeafLightBrush { get; } = new(Color.FromArgb(64, 158, 72));
    public SolidBrush RockBaseBrush { get; } = new(Color.FromArgb(124, 130, 140));
    public SolidBrush CopperGlintBrush { get; } = new(Color.FromArgb(218, 124, 54));
    public SolidBrush IronGlintBrush { get; } = new(Color.FromArgb(196, 206, 220));
    public SolidBrush GoldGlintBrush { get; } = new(Color.FromArgb(245, 204, 58));
    public SolidBrush WoodWallBrush { get; } = new(Color.FromArgb(148, 98, 52));
    public SolidBrush StoneWallBrush { get; } = new(Color.FromArgb(108, 116, 128));
    public SolidBrush WorkbenchBrush { get; } = new(Color.FromArgb(176, 118, 58));
    public SolidBrush TorchFlameBrush { get; } = new(Color.FromArgb(255, 198, 66));
    public SolidBrush TorchGlowBrush { get; } = new(Color.FromArgb(52, 255, 204, 96));
    public SolidBrush PlayerGlowBrush { get; } = new(Color.FromArgb(44, 210, 232, 255));
    public SolidBrush PlayerBodyBrush { get; } = new(Color.FromArgb(66, 165, 245));
    public SolidBrush PlayerVisorBrush { get; } = new(Color.FromArgb(245, 248, 255));
    public SolidBrush SlimeBrush { get; } = new(Color.FromArgb(104, 212, 92));
    public SolidBrush RaiderBrush { get; } = new(Color.FromArgb(192, 74, 74));
    public SolidBrush ArcherBrush { get; } = new(Color.FromArgb(216, 212, 198));
    public SolidBrush BossBrush { get; } = new(Color.FromArgb(164, 48, 186));
    public SolidBrush BossEnragedBrush { get; } = new(Color.FromArgb(236, 46, 72));
    public SolidBrush PlayerProjectileBrush { get; } = new(Color.FromArgb(255, 236, 120));
    public SolidBrush EnemyProjectileBrush { get; } = new(Color.FromArgb(255, 88, 108));
    public SolidBrush DropBrush { get; } = new(Color.FromArgb(246, 206, 86));
    public SolidBrush TextWhiteBrush { get; } = new(Color.FromArgb(242, 246, 252));
    public SolidBrush TextGoldBrush { get; } = new(Color.FromArgb(248, 202, 88));
    public SolidBrush TextMutedBrush { get; } = new(Color.FromArgb(172, 186, 206));
    public SolidBrush TextGreenBrush { get; } = new(Color.FromArgb(118, 230, 146));
    public SolidBrush BuildValidBrush { get; } = new(Color.FromArgb(72, 86, 234, 128));
    public SolidBrush BuildInvalidBrush { get; } = new(Color.FromArgb(72, 236, 76, 86));
    public SolidBrush ShoreFoamBrush { get; } = new(Color.FromArgb(115, 224, 244, 255));
    public SolidBrush MinimapPlayerBrush { get; } = new(Color.FromArgb(64, 224, 255));
    public SolidBrush MinimapEnemyBrush { get; } = new(Color.FromArgb(246, 72, 82));
    public SolidBrush MinimapBossBrush { get; } = new(Color.FromArgb(255, 210, 62));

    public Pen TileGridPen { get; } = new(Color.FromArgb(20, 0, 0, 0), 1f);
    public Pen DarkOutlinePen { get; } = new(Color.FromArgb(22, 26, 36), 1.5f);
    public Pen HudBorderPen { get; } = new(Color.FromArgb(95, 118, 152), 1.5f);
    public Pen HotbarSelectedPen { get; } = new(Color.FromArgb(248, 204, 84), 2.5f);
    public Pen SwingArcPen { get; } = new(Color.FromArgb(220, 255, 242, 166), 3.5f);
    public Pen BossAuraPen { get; } = new(Color.FromArgb(190, 255, 76, 98), 2.5f);
    public Pen BuildValidPen { get; } = new(Color.FromArgb(210, 110, 248, 148), 2f);
    public Pen BuildInvalidPen { get; } = new(Color.FromArgb(210, 248, 96, 108), 2f);
    public Pen MinimapViewportPen { get; } = new(Color.FromArgb(190, 255, 255, 255), 1f);

    public Font HudTitleFont { get; } = new("Segoe UI", 10.5f, FontStyle.Bold);
    public Font HudBodyFont { get; } = new("Segoe UI", 9.0f, FontStyle.Regular);
    public Font SmallFont { get; } = new("Segoe UI", 7.8f, FontStyle.Bold);
    public Font BossTitleFont { get; } = new("Segoe UI", 11.0f, FontStyle.Bold);

    /// <summary>Initializes pre-cached alpha brushes for smooth zero-allocation night lighting.</summary>
    public RenderResourceCache()
    {
        for (int i = 0; i <= NIGHT_ALPHA_BUCKETS; i++)
        {
            int alpha = (int)MathF.Round((i / (float)NIGHT_ALPHA_BUCKETS) * 150f);
            _nightOverlayBrushes[i] = new SolidBrush(Color.FromArgb(alpha, 10, 16, 42));
        }
    }

    /// <summary>Gets or caches a <see cref="SolidBrush"/> parsed from a hex color string (e.g., "#4E9A3F").</summary>
    public SolidBrush GetHexBrush(string colorHex)
    {
        if (_hexBrushes.TryGetValue(colorHex, out SolidBrush? cached))
        {
            return cached;
        }

        Color parsed = ColorTranslator.FromHtml(colorHex);
        var brush = new SolidBrush(parsed);
        _hexBrushes[colorHex] = brush;
        return brush;
    }

    /// <summary>Gets a pre-allocated dark night overlay brush for normalized darkness factor in [0, 1].</summary>
    public SolidBrush GetNightOverlayBrush(float darknessFactor)
    {
        float clamped = Math.Clamp(darknessFactor, 0f, 1f);
        int bucket = (int)MathF.Round(clamped * NIGHT_ALPHA_BUCKETS);
        return _nightOverlayBrushes[bucket];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (SolidBrush b in _hexBrushes.Values) b.Dispose();
        _hexBrushes.Clear();
        for (int i = 0; i < _nightOverlayBrushes.Length; i++) _nightOverlayBrushes[i].Dispose();

        ShadowBrush.Dispose();
        HudPanelBrush.Dispose();
        HpBackgroundBrush.Dispose();
        HpFillBrush.Dispose();
        BossHpFillBrush.Dispose();
        EnemyHpFillBrush.Dispose();
        ResourceHpFillBrush.Dispose();
        HotbarSlotBrush.Dispose();
        HotbarSelectedBrush.Dispose();
        TreeTrunkBrush.Dispose();
        TreeLeafDarkBrush.Dispose();
        TreeLeafLightBrush.Dispose();
        RockBaseBrush.Dispose();
        CopperGlintBrush.Dispose();
        IronGlintBrush.Dispose();
        GoldGlintBrush.Dispose();
        WoodWallBrush.Dispose();
        StoneWallBrush.Dispose();
        WorkbenchBrush.Dispose();
        TorchFlameBrush.Dispose();
        TorchGlowBrush.Dispose();
        PlayerGlowBrush.Dispose();
        PlayerBodyBrush.Dispose();
        PlayerVisorBrush.Dispose();
        SlimeBrush.Dispose();
        RaiderBrush.Dispose();
        ArcherBrush.Dispose();
        BossBrush.Dispose();
        BossEnragedBrush.Dispose();
        PlayerProjectileBrush.Dispose();
        EnemyProjectileBrush.Dispose();
        DropBrush.Dispose();
        TextWhiteBrush.Dispose();
        TextGoldBrush.Dispose();
        TextMutedBrush.Dispose();
        TextGreenBrush.Dispose();
        BuildValidBrush.Dispose();
        BuildInvalidBrush.Dispose();
        ShoreFoamBrush.Dispose();
        MinimapPlayerBrush.Dispose();
        MinimapEnemyBrush.Dispose();
        MinimapBossBrush.Dispose();

        TileGridPen.Dispose();
        DarkOutlinePen.Dispose();
        HudBorderPen.Dispose();
        HotbarSelectedPen.Dispose();
        SwingArcPen.Dispose();
        BossAuraPen.Dispose();
        BuildValidPen.Dispose();
        BuildInvalidPen.Dispose();
        MinimapViewportPen.Dispose();

        HudTitleFont.Dispose();
        HudBodyFont.Dispose();
        SmallFont.Dispose();
        BossTitleFont.Dispose();

        _disposed = true;
    }
}
