using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.UI;
using MiniNecesse.App.Views;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Modern, dark-minimalist GDI+ leaderboard dialog presenting Top 10 high scores
/// isolated per (<see cref="GameMode"/> × <see cref="Difficulty"/>) bucket.
/// Features DPI-responsive layout, vector Top-3 medals, hover &amp; record highlights,
/// smooth entrance transition, and full keyboard navigation (←/→, ↑/↓/Tab, Esc).
/// </summary>
public sealed class FrmHighScoresDialog : Form
{
    private const float OPEN_ANIMATION_MS = 140f;

    private static readonly GameMode[] MODES = { GameMode.Campaign, GameMode.Endless };
    private static readonly Difficulty[] DIFFICULTIES = { Difficulty.Tutorial, Difficulty.Normal, Difficulty.Hard };

    private readonly IHighScoreRepository _repository;
    private readonly HighScoreEntry? _highlightedEntry;
    private readonly List<Font> _ownedFonts = new();

    private readonly Label _lblTitle;
    private readonly Label _lblSubtitle;
    private readonly Label _lblModeCaption;
    private readonly Label _lblDiffCaption;
    private readonly SegmentedTabControl _modeTabs;
    private readonly SegmentedTabControl _diffTabs;
    private readonly LeaderboardCanvasControl _leaderboardCanvas;
    private readonly Label _lblKeyboardHint;
    private readonly Button _btnClose;
    private readonly System.Windows.Forms.Timer _animTimer;

    private readonly Stopwatch _animStopwatch = new();
    private bool _layoutReady;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="FrmHighScoresDialog"/> class.</summary>
    public FrmHighScoresDialog(
        IHighScoreRepository highScoreRepository,
        GameMode initialMode = GameMode.Campaign,
        Difficulty initialDifficulty = Difficulty.Normal,
        HighScoreEntry? highlightedEntry = null)
    {
        ArgumentNullException.ThrowIfNull(highScoreRepository);
        _repository = highScoreRepository;
        _highlightedEntry = highlightedEntry;

        SuspendLayout();
        Text = "MiniNecesse — Leaderboard";
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = UiTheme.BackgroundPrimary;
        ForeColor = UiTheme.TextPrimary;
        Font = CreateOwnedFont(UiTheme.FontFamily, 10f);

        _lblTitle = new Label
        {
            Text = "TOP 10 SURVIVOR LEADERBOARD",
            AutoSize = false,
            UseMnemonic = false,
            Height = 36,
            Font = CreateOwnedFont(UiTheme.FontFamily, 15.5f, FontStyle.Bold),
            ForeColor = UiTheme.AccentGold,
            BackColor = UiTheme.BackgroundPrimary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblSubtitle = new Label
        {
            Text = "Scores are tracked independently per World Mode and Difficulty.",
            AutoSize = false,
            UseMnemonic = false,
            Height = 24,
            Font = CreateOwnedFont(UiTheme.FontFamily, 9.25f),
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.BackgroundPrimary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblModeCaption = new Label
        {
            Text = "WORLD MODE",
            AutoSize = false,
            Height = 18,
            Font = CreateOwnedFont(UiTheme.FontFamily, 8f, FontStyle.Bold),
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.BackgroundPrimary
        };

        _lblDiffCaption = new Label
        {
            Text = "DIFFICULTY",
            AutoSize = false,
            Height = 18,
            Font = CreateOwnedFont(UiTheme.FontFamily, 8f, FontStyle.Bold),
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.BackgroundPrimary
        };

        _modeTabs = new SegmentedTabControl();
        _modeTabs.SetOptions("Campaign", "Endless");
        _modeTabs.SelectedIndex = initialMode == GameMode.Endless ? 1 : 0;

        _diffTabs = new SegmentedTabControl();
        _diffTabs.SetOptions("Tutorial", "Normal", "Hard");
        _diffTabs.SelectedIndex = ResolveDifficultyTabIndex(initialDifficulty);

        _leaderboardCanvas = new LeaderboardCanvasControl
        {
            AnimationProgress = 0f
        };

        _lblKeyboardHint = new Label
        {
            Text = "[← / →] Difficulty   •   [↑ / ↓ or Tab] World Mode   •   [Esc] Close",
            AutoSize = false,
            Height = 24,
            Font = CreateOwnedFont(UiTheme.FontFamily, 8.75f),
            ForeColor = UiTheme.TextMuted,
            BackColor = UiTheme.BackgroundPrimary,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _btnClose = new Button
        {
            Text = "Close",
            Size = new Size(108, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = UiTheme.ButtonNeutral,
            ForeColor = UiTheme.TextPrimary,
            Font = CreateOwnedFont(UiTheme.FontFamily, 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnClose.FlatAppearance.BorderColor = UiTheme.BorderSubtle;
        _btnClose.FlatAppearance.BorderSize = 1;

        Controls.AddRange(new Control[]
        {
            _lblTitle,
            _lblSubtitle,
            _lblModeCaption,
            _lblDiffCaption,
            _modeTabs,
            _diffTabs,
            _leaderboardCanvas,
            _lblKeyboardHint,
            _btnClose
        });

        _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
        SubscribeEvents();
        ClientSize = new Size(820, 640);
        MinimumSize = new Size(680, 560);
        _layoutReady = true;
        LayoutControls();
        ResumeLayout(false);

        ReloadActiveBucket();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _leaderboardCanvas.AnimationProgress = 0f;
        _animStopwatch.Restart();
        _animTimer.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutControls();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Left:
                _diffTabs.SelectPrevious();
                return true;
            case Keys.Right:
                _diffTabs.SelectNext();
                return true;
            case Keys.Up:
            case Keys.Down:
            case Keys.Tab:
                _modeTabs.SelectNext();
                return true;
            case Keys.Escape:
                Close();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _animTimer.Stop();
        _animStopwatch.Stop();
        UnsubscribeEvents();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _animTimer.Dispose();
            for (int i = 0; i < _ownedFonts.Count; i++)
            {
                _ownedFonts[i].Dispose();
            }

            _ownedFonts.Clear();
        }

        base.Dispose(disposing);
    }

    private void SubscribeEvents()
    {
        _modeTabs.SelectedIndexChanged += HandleFilterChanged;
        _diffTabs.SelectedIndexChanged += HandleFilterChanged;
        _btnClose.Click += HandleCloseClick;
        _animTimer.Tick += HandleAnimTick;
    }

    private void UnsubscribeEvents()
    {
        _modeTabs.SelectedIndexChanged -= HandleFilterChanged;
        _diffTabs.SelectedIndexChanged -= HandleFilterChanged;
        _btnClose.Click -= HandleCloseClick;
        _animTimer.Tick -= HandleAnimTick;
    }

    private void HandleFilterChanged(object? sender, EventArgs e) => ReloadActiveBucket();
    private void HandleCloseClick(object? sender, EventArgs e) => Close();

    private void HandleAnimTick(object? sender, EventArgs e)
    {
        float progress = Math.Clamp(_animStopwatch.ElapsedMilliseconds / OPEN_ANIMATION_MS, 0f, 1f);
        _leaderboardCanvas.AnimationProgress = progress;

        if (progress >= 1f)
        {
            _animTimer.Stop();
            _animStopwatch.Stop();
        }
    }

    private void LayoutControls()
    {
        if (!_layoutReady)
        {
            return;
        }

        int pad = UiTheme.PagePadding;
        int contentW = Math.Max(320, ClientSize.Width - (pad * 2));
        int contentH = Math.Max(320, ClientSize.Height - (pad * 2));

        _lblTitle.SetBounds(pad, pad, contentW, 36);
        _lblSubtitle.SetBounds(pad, _lblTitle.Bottom + 2, contentW, 24);

        int filtersTop = _lblSubtitle.Bottom + UiTheme.SectionGap;
        int modeWidth = Math.Max(190, (int)(contentW * 0.36f));
        int gap = 16;
        int diffWidth = Math.Max(220, contentW - modeWidth - gap);

        _lblModeCaption.SetBounds(pad, filtersTop, modeWidth, 18);
        _lblDiffCaption.SetBounds(pad + modeWidth + gap, filtersTop, diffWidth, 18);

        _modeTabs.SetBounds(pad, _lblModeCaption.Bottom + 4, modeWidth, 36);
        _diffTabs.SetBounds(pad + modeWidth + gap, _lblDiffCaption.Bottom + 4, diffWidth, 36);

        int footerTop = pad + contentH - 36;
        _btnClose.SetBounds(pad + contentW - _btnClose.Width, footerTop, _btnClose.Width, 34);
        _lblKeyboardHint.SetBounds(pad, footerTop + 5, Math.Max(120, _btnClose.Left - pad - 12), 24);

        int canvasTop = _modeTabs.Bottom + UiTheme.SectionGap;
        int canvasHeight = Math.Max(180, footerTop - canvasTop - UiTheme.SectionGap);
        _leaderboardCanvas.SetBounds(pad, canvasTop, contentW, canvasHeight);
    }

    private void ReloadActiveBucket()
    {
        GameMode mode = MODES[Math.Clamp(_modeTabs.SelectedIndex, 0, MODES.Length - 1)];
        Difficulty diff = DIFFICULTIES[Math.Clamp(_diffTabs.SelectedIndex, 0, DIFFICULTIES.Length - 1)];
        _lblSubtitle.Text = diff == Difficulty.Tutorial
            ? $"Showing {mode} • {diff} scores (kept separate from Normal & Hard runs)."
            : $"Showing Top 10 survivors for {mode} • {diff}.";

        try
        {
            IReadOnlyList<HighScoreEntry> entries = _repository.LoadTopScores(mode, diff);
            string emptyMsg = diff == Difficulty.Tutorial
                ? "Complete a Tutorial run to record an onboarding practice score."
                : $"Complete a {mode} ({diff}) run to claim rank #1!";
            _leaderboardCanvas.SetEntries(entries, _highlightedEntry, emptyMsg);
        }
        catch (SaveCorruptedException)
        {
            _leaderboardCanvas.SetEntries(Array.Empty<HighScoreEntry>(), null, "High score data unavailable.");
        }
    }

    private Font CreateOwnedFont(string family, float size, FontStyle style = FontStyle.Regular)
    {
        var font = new Font(family, size, style);
        _ownedFonts.Add(font);
        return font;
    }

    private static int ResolveDifficultyTabIndex(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Tutorial => 0,
            Difficulty.Hard => 2,
            _ => 1
        };
    }
}
