using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using MiniNecesse.App.UI;

namespace MiniNecesse.App.Views;

/// <summary>
/// Owner-drawn GDI+ segmented pill selector with hover feedback, cached geometry,
/// and zero allocations inside <see cref="OnPaint"/>.
/// </summary>
public sealed class SegmentedTabControl : Control
{
    private const int SEGMENT_PADDING = 3;

    private readonly UiThemeResources _theme = new();
    private string[] _options = Array.Empty<string>();
    private GraphicsPath? _containerPath;
    private GraphicsPath[] _segmentPaths = Array.Empty<GraphicsPath>();
    private Rectangle[] _segmentBounds = Array.Empty<Rectangle>();
    private int _selectedIndex;
    private int _hoverIndex = -1;

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_options.Length == 0)
            {
                return;
            }

            int clamped = Math.Clamp(value, 0, _options.Length - 1);
            if (clamped == _selectedIndex)
            {
                return;
            }

            _selectedIndex = clamped;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SegmentedTabControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = UiTheme.BackgroundPrimary;
        Cursor = Cursors.Hand;
        Height = 36;
    }

    public void SetOptions(params string[] options)
    {
        _options = options ?? Array.Empty<string>();
        _selectedIndex = _options.Length > 0 ? Math.Clamp(_selectedIndex, 0, _options.Length - 1) : 0;
        _hoverIndex = -1;
        RebuildGeometry();
        Invalidate();
    }

    public void SelectNext()
    {
        if (_options.Length == 0) return;
        SelectedIndex = (_selectedIndex + 1) % _options.Length;
    }

    public void SelectPrevious()
    {
        if (_options.Length == 0) return;
        SelectedIndex = (_selectedIndex - 1 + _options.Length) % _options.Length;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildGeometry();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hit = HitTestSegment(e.Location);
        if (hit != _hoverIndex)
        {
            _hoverIndex = hit;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        int hit = HitTestSegment(e.Location);
        if (hit >= 0)
        {
            SelectedIndex = hit;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_containerPath is not null)
        {
            g.FillPath(_theme.HeaderBandBrush, _containerPath);
            g.DrawPath(_theme.BorderSubtlePen, _containerPath);
        }

        for (int i = 0; i < _options.Length; i++)
        {
            bool selected = i == _selectedIndex;
            bool hovered = !selected && i == _hoverIndex;
            GraphicsPath? segPath = i < _segmentPaths.Length ? _segmentPaths[i] : null;
            Rectangle rect = i < _segmentBounds.Length ? _segmentBounds[i] : Rectangle.Empty;

            if (segPath is not null)
            {
                if (selected)
                {
                    g.FillPath(_theme.AccentGoldBrush, segPath);
                }
                else if (hovered)
                {
                    g.FillPath(_theme.RowHoverBrush, segPath);
                }
            }

            SolidBrush textBrush = selected
                ? _theme.TextDarkBrush
                : (hovered ? _theme.TextPrimaryBrush : _theme.TextSecondaryBrush);
            g.DrawString(_options[i], _theme.HeaderFont, textBrush, rect, _theme.CenterMiddleFormat);
        }
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

    private int HitTestSegment(Point pt)
    {
        for (int i = 0; i < _segmentBounds.Length; i++)
        {
            if (_segmentBounds[i].Contains(pt))
            {
                return i;
            }
        }

        return -1;
    }

    private void RebuildGeometry()
    {
        DisposeGeometry();
        if (ClientSize.Width <= 4 || ClientSize.Height <= 4 || _options.Length == 0)
        {
            return;
        }

        var outer = new Rectangle(1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        _containerPath = UiTheme.CreateRoundedRectPath(outer, UiTheme.ControlRadius);

        int innerWidth = Math.Max(1, outer.Width - (SEGMENT_PADDING * 2));
        int segHeight = Math.Max(1, outer.Height - (SEGMENT_PADDING * 2));
        int count = _options.Length;
        float step = innerWidth / (float)count;

        _segmentBounds = new Rectangle[count];
        _segmentPaths = new GraphicsPath[count];

        for (int i = 0; i < count; i++)
        {
            int x = outer.X + SEGMENT_PADDING + (int)MathF.Round(i * step);
            int nextX = (i == count - 1)
                ? outer.Right - SEGMENT_PADDING
                : outer.X + SEGMENT_PADDING + (int)MathF.Round((i + 1) * step);
            var segRect = new Rectangle(x, outer.Y + SEGMENT_PADDING, Math.Max(4, nextX - x - 1), segHeight);
            _segmentBounds[i] = segRect;
            _segmentPaths[i] = UiTheme.CreateRoundedRectPath(segRect, UiTheme.PillRadius);
        }
    }

    private void DisposeGeometry()
    {
        _containerPath?.Dispose();
        _containerPath = null;
        for (int i = 0; i < _segmentPaths.Length; i++)
        {
            _segmentPaths[i].Dispose();
        }

        _segmentPaths = Array.Empty<GraphicsPath>();
        _segmentBounds = Array.Empty<Rectangle>();
    }
}
