using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using MiniNecesse.App.UI;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Views;

/// <summary>
/// Custom owner-drawn GDI+ Top-10 leaderboard canvas with vector Top-3 medals,
/// newly-achieved record highlighting, hover states, empty-state illustration,
/// DPI-responsive geometry, and zero heap allocations inside <see cref="OnPaint"/>.
/// </summary>
public sealed class LeaderboardCanvasControl : Control
{
    private const int MAX_ROWS = 10;
    private const int HEADER_HEIGHT = 34;
    private const int INNER_MARGIN_X = 10;

    private readonly UiThemeResources _theme = new();
    private readonly List<FormattedRow> _rows = new(MAX_ROWS);

    private GraphicsPath? _cardPath;
    private GraphicsPath? _headerPath;
    private GraphicsPath? _newBadgeTemplatePath;
    private GraphicsPath? _emptyTrophyPath;

    private readonly Rectangle[] _rowRects = new Rectangle[MAX_ROWS];
    private readonly GraphicsPath?[] _rowPaths = new GraphicsPath?[MAX_ROWS];
    private readonly Rectangle[] _medalRects = new Rectangle[3];
    private readonly PointF[][] _leftRibbonPolys = { new PointF[3], new PointF[3], new PointF[3] };
    private readonly PointF[][] _rightRibbonPolys = { new PointF[3], new PointF[3], new PointF[3] };
    private readonly Rectangle[] _sheenRects = new Rectangle[3];

    private Rectangle _headerBandRect;
    private Rectangle _colRankRect;
    private Rectangle _colPlayerRect;
    private Rectangle _colScoreRect;
    private Rectangle _colTimeRect;
    private Rectangle _colDateRect;
    private Rectangle _emptyTitleRect;
    private Rectangle _emptySubtitleRect;

    private string _emptySubtitleText = "Complete a run in this Mode & Difficulty to claim rank #1!";
    private int _hoverRowIndex = -1;
    private float _animationProgress = 1f;

    public float AnimationProgress
    {
        get => _animationProgress;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (MathF.Abs(clamped - _animationProgress) > 0.001f)
            {
                _animationProgress = clamped;
                Invalidate();
            }
        }
    }

    public LeaderboardCanvasControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = UiTheme.BackgroundPrimary;
    }

    public void SetEntries(
        IReadOnlyList<HighScoreEntry> entries,
        HighScoreEntry? highlightedEntry = null,
        string? emptySubtitle = null)
    {
        _rows.Clear();
        _hoverRowIndex = -1;
        if (!string.IsNullOrWhiteSpace(emptySubtitle))
        {
            _emptySubtitleText = emptySubtitle;
        }

        if (entries is not null)
        {
            int count = Math.Min(MAX_ROWS, entries.Count);
            for (int i = 0; i < count; i++)
            {
                HighScoreEntry e = entries[i];
                bool isNew = highlightedEntry is not null && IsSameEntry(e, highlightedEntry);
                string timeText = e.PlayTimeSeconds > 0.0
                    ? FormatDuration(e.PlayTimeSeconds)
                    : "--";
                string dateText = e.AchievedAtUtc == DateTime.UnixEpoch
                    ? "--"
                    : e.AchievedAtUtc.ToLocalTime().ToString("yyyy-MM-dd");

                _rows.Add(new FormattedRow(
                    rankIndex: i,
                    rankLabel: $"#{i + 1}",
                    playerName: e.PlayerName,
                    scoreLabel: e.Score.ToString("N0"),
                    timeLabel: timeText,
                    dateLabel: dateText,
                    isHighlighted: isNew));
            }
        }

        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildGeometry();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTestRow(e.Location);
        if (hit != _hoverRowIndex)
        {
            _hoverRowIndex = hit;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverRowIndex != -1)
        {
            _hoverRowIndex = -1;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float ease = 1f - MathF.Pow(1f - _animationProgress, 3f);
        float slideY = (1f - ease) * 14f;
        if (slideY > 0.05f)
        {
            g.TranslateTransform(0f, slideY);
        }

        if (_cardPath is not null)
        {
            g.FillPath(_theme.CardBrush, _cardPath);
            g.DrawPath(_theme.BorderSubtlePen, _cardPath);
        }

        if (_headerPath is not null)
        {
            g.FillPath(_theme.HeaderBandBrush, _headerPath);
        }

        DrawColumnHeaders(g);

        if (_rows.Count == 0)
        {
            DrawEmptyState(g);
            g.ResetTransform();
            return;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            DrawRow(g, _rows[i], _rowRects[i], _rowPaths[i], hovered: i == _hoverRowIndex);
        }

        g.ResetTransform();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeGeometry();
            _theme.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawColumnHeaders(Graphics g)
    {
        g.DrawLine(_theme.BorderSubtlePen, _headerBandRect.Left + 2, _headerBandRect.Bottom, _headerBandRect.Right - 2, _headerBandRect.Bottom);
        g.DrawString("RANK", _theme.HeaderFont, _theme.TextMutedBrush, _colRankRect, _theme.CenterMiddleFormat);
        g.DrawString("SURVIVOR", _theme.HeaderFont, _theme.TextMutedBrush, _colPlayerRect, _theme.LeftMiddleFormat);
        g.DrawString("SCORE", _theme.HeaderFont, _theme.TextMutedBrush, _colScoreRect, _theme.RightMiddleFormat);
        g.DrawString("TIME", _theme.HeaderFont, _theme.TextMutedBrush, _colTimeRect, _theme.CenterMiddleFormat);
        g.DrawString("DATE", _theme.HeaderFont, _theme.TextMutedBrush, _colDateRect, _theme.RightMiddleFormat);
    }

    private void DrawRow(Graphics g, FormattedRow row, Rectangle rect, GraphicsPath? rowPath, bool hovered)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;

        SolidBrush bgBrush = row.IsHighlighted
            ? (hovered ? _theme.RowHighlightHoverBrush : _theme.RowHighlightBrush)
            : (hovered ? _theme.RowHoverBrush : (row.RankIndex % 2 == 0 ? _theme.RowEvenBrush : _theme.RowOddBrush));

        if (rowPath is not null)
        {
            g.FillPath(bgBrush, rowPath);
            if (row.IsHighlighted)
            {
                g.DrawPath(_theme.AccentGoldPen, rowPath);
                g.FillRectangle(_theme.AccentGoldBrush, rect.Left + 1, rect.Top + 4, 4, rect.Height - 8);
            }
        }

        if (row.RankIndex < 3)
        {
            DrawVectorMedal(g, row.RankIndex);
        }
        else
        {
            var rRank = new Rectangle(_colRankRect.X, rect.Y, _colRankRect.Width, rect.Height);
            g.DrawString(row.RankLabel, _theme.RowPrimaryFont, _theme.TextMutedBrush, rRank, _theme.CenterMiddleFormat);
        }

        Rectangle rPlayer = new Rectangle(_colPlayerRect.X, rect.Y, _colPlayerRect.Width - (row.IsHighlighted ? 44 : 0), rect.Height);
        SolidBrush nameBrush = row.IsHighlighted || row.RankIndex < 3 ? _theme.TextPrimaryBrush : _theme.TextSecondaryBrush;
        g.DrawString(row.PlayerName, row.RankIndex < 3 || row.IsHighlighted ? _theme.RowPrimaryFont : _theme.RowBodyFont, nameBrush, rPlayer, _theme.LeftMiddleFormat);

        if (row.IsHighlighted && _newBadgeTemplatePath is not null)
        {
            int badgeX = rPlayer.Right + 4;
            int badgeY = rect.Y + ((rect.Height - 16) / 2);
            g.TranslateTransform(badgeX, badgeY);
            g.FillPath(_theme.AccentGoldBrush, _newBadgeTemplatePath);
            g.DrawString("NEW", _theme.BadgeFont, _theme.TextDarkBrush, new RectangleF(0f, 0f, 36f, 16f), _theme.CenterMiddleFormat);
            g.TranslateTransform(-badgeX, -badgeY);
        }

        var rScore = new Rectangle(_colScoreRect.X, rect.Y, _colScoreRect.Width, rect.Height);
        g.DrawString(row.ScoreLabel, _theme.RowPrimaryFont, _theme.AccentGoldBrush, rScore, _theme.RightMiddleFormat);

        var rTime = new Rectangle(_colTimeRect.X, rect.Y, _colTimeRect.Width, rect.Height);
        g.DrawString(row.TimeLabel, _theme.RowBodyFont, _theme.TextSecondaryBrush, rTime, _theme.CenterMiddleFormat);

        var rDate = new Rectangle(_colDateRect.X, rect.Y, _colDateRect.Width, rect.Height);
        g.DrawString(row.DateLabel, _theme.RowBodyFont, _theme.TextMutedBrush, rDate, _theme.RightMiddleFormat);
    }

    private void DrawVectorMedal(Graphics g, int rankIndex)
    {
        g.FillPolygon(_theme.RibbonBrush, _leftRibbonPolys[rankIndex]);
        g.FillPolygon(_theme.RibbonBrush, _rightRibbonPolys[rankIndex]);

        SolidBrush medalFill = rankIndex == 0
            ? _theme.MedalGoldBrush
            : (rankIndex == 1 ? _theme.MedalSilverBrush : _theme.MedalBronzeBrush);
        Pen rimPen = rankIndex == 0
            ? _theme.MedalGoldRimPen
            : (rankIndex == 1 ? _theme.MedalSilverRimPen : _theme.MedalBronzeRimPen);

        Rectangle coin = _medalRects[rankIndex];
        g.FillEllipse(medalFill, coin);
        g.FillEllipse(_theme.HighlightSheenBrush, _sheenRects[rankIndex]);
        g.DrawEllipse(rimPen, coin);

        string digit = rankIndex == 0 ? "1" : (rankIndex == 1 ? "2" : "3");
        g.DrawString(digit, _theme.MedalNumberFont, _theme.TextDarkBrush, coin, _theme.CenterMiddleFormat);
    }

    private void DrawEmptyState(Graphics g)
    {
        if (_emptyTrophyPath is not null)
        {
            g.DrawPath(_theme.TrophyOutlinePen, _emptyTrophyPath);
        }

        g.DrawString("No scores recorded yet", _theme.EmptyTitleFont, _theme.TextSecondaryBrush, _emptyTitleRect, _theme.CenterMiddleFormat);
        g.DrawString(_emptySubtitleText, _theme.RowBodyFont, _theme.TextMutedBrush, _emptySubtitleRect, _theme.CenterMiddleFormat);
    }

    private int HitTestRow(Point pt)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rowRects[i].Contains(pt))
            {
                return i;
            }
        }

        return -1;
    }

    private void RebuildGeometry()
    {
        DisposeGeometry();
        if (ClientSize.Width < 120 || ClientSize.Height < 120)
        {
            return;
        }

        var cardRect = new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        _cardPath = UiTheme.CreateRoundedRectPath(cardRect, UiTheme.CardRadius);

        _headerBandRect = new Rectangle(cardRect.X + 2, cardRect.Y + 2, cardRect.Width - 4, HEADER_HEIGHT);
        _headerPath = UiTheme.CreateRoundedRectPath(_headerBandRect, UiTheme.CardRadius - 2);

        int contentX = cardRect.X + INNER_MARGIN_X;
        int contentW = Math.Max(80, cardRect.Width - (INNER_MARGIN_X * 2));
        int rankW = 54;
        int dateW = 94;
        int timeW = 72;
        int scoreW = 92;
        int playerW = Math.Max(80, contentW - rankW - dateW - timeW - scoreW - 20);

        _colRankRect = new Rectangle(contentX, _headerBandRect.Y, rankW, HEADER_HEIGHT);
        _colPlayerRect = new Rectangle(_colRankRect.Right + 6, _headerBandRect.Y, playerW, HEADER_HEIGHT);
        _colScoreRect = new Rectangle(_colPlayerRect.Right + 4, _headerBandRect.Y, scoreW, HEADER_HEIGHT);
        _colTimeRect = new Rectangle(_colScoreRect.Right + 6, _headerBandRect.Y, timeW, HEADER_HEIGHT);
        _colDateRect = new Rectangle(_colTimeRect.Right + 4, _headerBandRect.Y, dateW, HEADER_HEIGHT);

        int bodyTop = _headerBandRect.Bottom + 6;
        int bodyHeight = Math.Max(40, cardRect.Bottom - bodyTop - 8);
        int slotHeight = bodyHeight / MAX_ROWS;
        int rowHeight = Math.Max(18, slotHeight - 3);

        for (int i = 0; i < MAX_ROWS; i++)
        {
            int ry = bodyTop + (i * slotHeight);
            var rr = new Rectangle(contentX, ry, contentW, rowHeight);
            _rowRects[i] = rr;
            _rowPaths[i] = UiTheme.CreateRoundedRectPath(rr, UiTheme.PillRadius);

            if (i < 3)
            {
                int medalDiameter = Math.Clamp(rowHeight - 8, 16, 24);
                int mx = _colRankRect.X + ((_colRankRect.Width - medalDiameter) / 2);
                int my = rr.Y + ((rr.Height - medalDiameter) / 2);
                _medalRects[i] = new Rectangle(mx, my, medalDiameter, medalDiameter);
                _sheenRects[i] = new Rectangle(mx + 3, my + 2, Math.Max(4, medalDiameter / 2), Math.Max(3, medalDiameter / 3));

                float cx = mx + (medalDiameter * 0.5f);
                float cyBottom = my + medalDiameter - 1f;
                _leftRibbonPolys[i][0] = new PointF(cx - 2f, cyBottom - 3f);
                _leftRibbonPolys[i][1] = new PointF(cx - 8f, cyBottom + 5f);
                _leftRibbonPolys[i][2] = new PointF(cx - 1f, cyBottom + 3f);

                _rightRibbonPolys[i][0] = new PointF(cx + 2f, cyBottom - 3f);
                _rightRibbonPolys[i][1] = new PointF(cx + 8f, cyBottom + 5f);
                _rightRibbonPolys[i][2] = new PointF(cx + 1f, cyBottom + 3f);
            }
        }

        _newBadgeTemplatePath = UiTheme.CreateRoundedRectPath(new Rectangle(0, 0, 36, 16), 4);

        // Centered vector trophy illustration + text rectangles for empty state
        int centerX = cardRect.X + (cardRect.Width / 2);
        int centerY = bodyTop + (bodyHeight / 2) - 18;
        _emptyTrophyPath = BuildTrophyVectorPath(centerX, centerY - 36);
        _emptyTitleRect = new Rectangle(contentX, centerY + 14, contentW, 28);
        _emptySubtitleRect = new Rectangle(contentX, centerY + 42, contentW, 24);
    }

    private void DisposeGeometry()
    {
        _cardPath?.Dispose();
        _cardPath = null;
        _headerPath?.Dispose();
        _headerPath = null;
        _newBadgeTemplatePath?.Dispose();
        _newBadgeTemplatePath = null;
        _emptyTrophyPath?.Dispose();
        _emptyTrophyPath = null;

        for (int i = 0; i < _rowPaths.Length; i++)
        {
            _rowPaths[i]?.Dispose();
            _rowPaths[i] = null;
        }
    }

    private static GraphicsPath BuildTrophyVectorPath(int cx, int cy)
    {
        var path = new GraphicsPath();
        // Cup chalice
        path.AddArc(cx - 20, cy - 18, 40, 34, 0, 180);
        path.AddLine(cx - 20, cy - 1, cx - 20, cy - 18);
        path.AddLine(cx - 20, cy - 18, cx + 20, cy - 18);
        path.AddLine(cx + 20, cy - 18, cx + 20, cy - 1);
        // Stem and base
        path.StartFigure();
        path.AddLine(cx, cy + 16, cx, cy + 28);
        path.StartFigure();
        path.AddLine(cx - 14, cy + 28, cx + 14, cy + 28);
        return path;
    }

    private static string FormatDuration(double totalSeconds)
    {
        int secs = Math.Max(0, (int)Math.Round(totalSeconds));
        int mins = secs / 60;
        int rem = secs % 60;
        return $"{mins:00}:{rem:00}";
    }

    private static bool IsSameEntry(HighScoreEntry a, HighScoreEntry b)
    {
        return a.Score == b.Score &&
               string.Equals(a.PlayerName, b.PlayerName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Mode, b.Mode, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(a.Difficulty, b.Difficulty, StringComparison.OrdinalIgnoreCase) &&
               Math.Abs((a.AchievedAtUtc - b.AchievedAtUtc).TotalSeconds) <= 2.0;
    }

    private sealed class FormattedRow
    {
        public int RankIndex { get; }
        public string RankLabel { get; }
        public string PlayerName { get; }
        public string ScoreLabel { get; }
        public string TimeLabel { get; }
        public string DateLabel { get; }
        public bool IsHighlighted { get; }

        public FormattedRow(
            int rankIndex,
            string rankLabel,
            string playerName,
            string scoreLabel,
            string timeLabel,
            string dateLabel,
            bool isHighlighted)
        {
            RankIndex = rankIndex;
            RankLabel = rankLabel;
            PlayerName = playerName;
            ScoreLabel = scoreLabel;
            TimeLabel = timeLabel;
            DateLabel = dateLabel;
            IsHighlighted = isHighlighted;
        }
    }
}
