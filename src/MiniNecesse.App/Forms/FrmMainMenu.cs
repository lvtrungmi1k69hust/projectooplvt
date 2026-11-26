using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Audio;
using MiniNecesse.App.Settings;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Main Menu presentation screen providing independent World Mode (Campaign / Endless Seed)
/// and Difficulty (Tutorial / Normal / Hard) selection, Continue (Slot 1), High Scores, Settings, and Exit via Constructor DI.
/// </summary>
public sealed class FrmMainMenu : Form
{
    private readonly ILevelLoader _levelLoader;
    private readonly ISaveStorage _saveStorage;
    private readonly IHighScoreRepository _highScoreRepository;
    private readonly AudioManager _audioManager;
    private readonly AppSettings _settings;
    private readonly Func<GameMode, Difficulty, string, int, bool, Form>? _gameplayLauncher;

    private readonly ComboBox _cmbGameMode;
    private readonly ComboBox _cmbDifficulty;
    private readonly Label _lblWorldOption;
    private readonly ComboBox _cmbIslandSelector;
    private readonly TextBox _txtEndlessSeed;
    private readonly Button _btnRandomSeed;
    private readonly Button _btnNewGame;
    private readonly Button _btnContinue;
    private readonly Button _btnHighScores;
    private readonly Button _btnSettings;
    private readonly Button _btnExit;
    private readonly Label _lblStatus;
    private readonly List<Font> _ownedFonts = new();

    /// <summary>Initializes a new instance of the <see cref="FrmMainMenu"/> class via Constructor DI.</summary>
    public FrmMainMenu(
        ILevelLoader levelLoader,
        ISaveStorage saveStorage,
        IHighScoreRepository highScoreRepository,
        AudioManager audioManager,
        AppSettings settings,
        Func<GameMode, Difficulty, string, int, bool, Form>? gameplayLauncher = null)
    {
        ArgumentNullException.ThrowIfNull(levelLoader);
        ArgumentNullException.ThrowIfNull(saveStorage);
        ArgumentNullException.ThrowIfNull(highScoreRepository);
        ArgumentNullException.ThrowIfNull(audioManager);
        ArgumentNullException.ThrowIfNull(settings);

        _levelLoader = levelLoader;
        _saveStorage = saveStorage;
        _highScoreRepository = highScoreRepository;
        _audioManager = audioManager;
        _settings = settings;
        _gameplayLauncher = gameplayLauncher;

        Text = "MiniNecesse — 2D Top-Down Survival Crafting";
        Size = new Size(560, 620);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(18, 24, 34);
        ForeColor = Color.FromArgb(236, 240, 246);
        Font = CreateOwnedFont("Segoe UI", 10.5f);

        Controls.Add(new Label
        {
            Text = "MININECESSE",
            Location = new Point(0, 24),
            Size = new Size(544, 44),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = CreateOwnedFont("Segoe UI", 26f, FontStyle.Bold),
            ForeColor = Color.FromArgb(242, 190, 84)
        });

        Controls.Add(new Label
        {
            Text = "2D Top-Down Survival • Campaign & Endless • Island Bosses",
            Location = new Point(0, 72),
            Size = new Size(544, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = CreateOwnedFont("Segoe UI", 9.8f, FontStyle.Italic),
            ForeColor = Color.FromArgb(156, 174, 198)
        });

        Controls.Add(new Label
        {
            Text = "World Mode:",
            Location = new Point(110, 110),
            Size = new Size(146, 22),
            Font = CreateOwnedFont("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(204, 214, 228)
        });

        Controls.Add(new Label
        {
            Text = "Difficulty:",
            Location = new Point(268, 110),
            Size = new Size(146, 22),
            Font = CreateOwnedFont("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(204, 214, 228)
        });

        _cmbGameMode = new ComboBox
        {
            Location = new Point(110, 134),
            Size = new Size(146, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 39, 54),
            ForeColor = Color.White
        };
        _cmbGameMode.Items.AddRange(new object[] { GameMode.Campaign, GameMode.Endless });
        _cmbGameMode.SelectedItem = _settings.GameMode;
        Controls.Add(_cmbGameMode);

        _cmbDifficulty = new ComboBox
        {
            Location = new Point(268, 134),
            Size = new Size(146, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 39, 54),
            ForeColor = Color.White
        };
        _cmbDifficulty.Items.AddRange(new object[] { Difficulty.Tutorial, Difficulty.Normal, Difficulty.Hard });
        _cmbDifficulty.SelectedItem = _cmbDifficulty.Items.Contains(_settings.Difficulty)
            ? _settings.Difficulty
            : Difficulty.Normal;
        Controls.Add(_cmbDifficulty);

        _lblWorldOption = new Label
        {
            Text = "Starting Island:",
            Location = new Point(110, 174),
            AutoSize = true,
            Font = CreateOwnedFont("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(204, 214, 228)
        };
        Controls.Add(_lblWorldOption);

        _cmbIslandSelector = new ComboBox
        {
            Location = new Point(110, 198),
            Size = new Size(304, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 39, 54),
            ForeColor = Color.White
        };
        Controls.Add(_cmbIslandSelector);

        _txtEndlessSeed = new TextBox
        {
            Location = new Point(110, 198),
            Size = new Size(218, 30),
            BackColor = Color.FromArgb(30, 39, 54),
            ForeColor = Color.White,
            Text = _settings.EndlessSeed.ToString(),
            Visible = false
        };
        Controls.Add(_txtEndlessSeed);

        _btnRandomSeed = new Button
        {
            Text = "Random",
            Location = new Point(334, 197),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(74, 88, 114),
            ForeColor = Color.White,
            Font = CreateOwnedFont("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false
        };
        Controls.Add(_btnRandomSeed);

        _btnNewGame = CreateMenuButton("Play New Game", 248, Color.FromArgb(46, 156, 98));
        _btnContinue = CreateMenuButton("Continue (Load Save Slot 1)", 302, Color.FromArgb(48, 118, 190));
        _btnHighScores = CreateMenuButton("High Scores (Top 10)", 356, Color.FromArgb(156, 112, 42));
        _btnSettings = CreateMenuButton("Settings (Audio, Difficulty & Chunks)", 410, Color.FromArgb(74, 88, 114));
        _btnExit = CreateMenuButton("Exit", 464, Color.FromArgb(164, 58, 58));

        _lblStatus = new Label
        {
            Location = new Point(20, 524),
            Size = new Size(504, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = CreateOwnedFont("Segoe UI", 9f),
            ForeColor = Color.FromArgb(146, 166, 190)
        };

        Controls.AddRange(new Control[]
        {
            _btnNewGame, _btnContinue, _btnHighScores, _btnSettings, _btnExit, _lblStatus
        });

        LoadAvailableIslands();
        ApplyModeVisibility();
        RefreshContinueButtonState();
        SubscribeEvents();
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnsubscribeEvents();
        base.OnFormClosing(e);
    }

    private void SubscribeEvents()
    {
        _cmbGameMode.SelectedIndexChanged += HandleModeOrDifficultyChanged;
        _cmbDifficulty.SelectedIndexChanged += HandleModeOrDifficultyChanged;
        _btnRandomSeed.Click += HandleRandomSeedClick;
        _btnNewGame.Click += HandleNewGameClick;
        _btnContinue.Click += HandleContinueClick;
        _btnHighScores.Click += HandleHighScoresClick;
        _btnSettings.Click += HandleSettingsClick;
        _btnExit.Click += HandleExitClick;
    }

    private void UnsubscribeEvents()
    {
        _cmbGameMode.SelectedIndexChanged -= HandleModeOrDifficultyChanged;
        _cmbDifficulty.SelectedIndexChanged -= HandleModeOrDifficultyChanged;
        _btnRandomSeed.Click -= HandleRandomSeedClick;
        _btnNewGame.Click -= HandleNewGameClick;
        _btnContinue.Click -= HandleContinueClick;
        _btnHighScores.Click -= HandleHighScoresClick;
        _btnSettings.Click -= HandleSettingsClick;
        _btnExit.Click -= HandleExitClick;
    }

    private void LoadAvailableIslands()
    {
        IReadOnlyList<string> islandIds = _levelLoader.GetAvailableIslandIds();
        _cmbIslandSelector.Items.Clear();
        for (int i = 0; i < islandIds.Count; i++)
        {
            _cmbIslandSelector.Items.Add(islandIds[i]);
        }

        if (_cmbIslandSelector.Items.Count > 0)
        {
            _cmbIslandSelector.SelectedIndex = 0;
        }
    }

    private GameMode SelectedMode =>
        _cmbGameMode.SelectedItem is GameMode mode ? mode : GameMode.Campaign;

    private Difficulty SelectedDifficulty =>
        _cmbDifficulty.SelectedItem is Difficulty diff ? diff : Difficulty.Normal;

    private int ParseSelectedSeed()
    {
        string raw = _txtEndlessSeed.Text.Trim();
        if (int.TryParse(raw, out int numeric))
        {
            return numeric;
        }

        if (string.IsNullOrEmpty(raw))
        {
            return _settings.EndlessSeed;
        }

        // Deterministic stable string seed FNV-1a 32-bit hash for text seeds
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < raw.Length; i++)
            {
                hash ^= raw[i];
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    private void ApplyModeVisibility()
    {
        bool endless = SelectedMode == GameMode.Endless;
        _lblWorldOption.Text = endless ? "Endless World Seed:" : "Starting Island (Campaign):";
        _cmbIslandSelector.Visible = !endless;
        _txtEndlessSeed.Visible = endless;
        _btnRandomSeed.Visible = endless;
    }

    private void HandleModeOrDifficultyChanged(object? sender, EventArgs e)
    {
        _settings.GameMode = SelectedMode;
        _settings.Difficulty = SelectedDifficulty;
        ApplyModeVisibility();
        RefreshContinueButtonState();
    }

    private void HandleRandomSeedClick(object? sender, EventArgs e)
    {
        int seed = Random.Shared.Next(100000, 999999);
        _settings.EndlessSeed = seed;
        _txtEndlessSeed.Text = seed.ToString();
    }

    private void RefreshContinueButtonState()
    {
        bool hasSave = _saveStorage.Exists(1);
        _btnContinue.Enabled = hasSave;
        _lblStatus.Text = $"Mode: {SelectedMode} | Difficulty: {SelectedDifficulty} | View: {_settings.ViewDistanceChunks} ch | Save 1: {(hasSave ? "Available" : "Empty")}";
    }

    private void HandleNewGameClick(object? sender, EventArgs e)
    {
        GameMode mode = SelectedMode;
        Difficulty difficulty = SelectedDifficulty;
        int seed = ParseSelectedSeed();
        _settings.GameMode = mode;
        _settings.Difficulty = difficulty;
        _settings.EndlessSeed = seed;
        _settings.Save();

        string selectedIsland = _cmbIslandSelector.SelectedItem?.ToString() ?? "ISLAND_01";
        LaunchGameplay(mode, difficulty, selectedIsland, seed, loadSavedSlot: false);
    }

    private void HandleContinueClick(object? sender, EventArgs e)
    {
        LaunchGameplay(SelectedMode, SelectedDifficulty, "ISLAND_01", _settings.EndlessSeed, loadSavedSlot: true);
    }

    private void LaunchGameplay(GameMode mode, Difficulty difficulty, string islandId, int worldSeed, bool loadSavedSlot)
    {
        if (_gameplayLauncher is null)
        {
            MessageBox.Show(this, "Gameplay view is not configured yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Hide();
        try
        {
            using Form gameForm = _gameplayLauncher(mode, difficulty, islandId, worldSeed, loadSavedSlot);
            gameForm.ShowDialog(this);
        }
        finally
        {
            if (_cmbDifficulty.Items.Contains(_settings.Difficulty))
            {
                _cmbDifficulty.SelectedItem = _settings.Difficulty;
            }

            RefreshContinueButtonState();
            Show();
        }
    }

    private void HandleHighScoresClick(object? sender, EventArgs e)
    {
        using var dialog = new FrmHighScoresDialog(_highScoreRepository);
        dialog.ShowDialog(this);
    }

    private void HandleSettingsClick(object? sender, EventArgs e)
    {
        using var dialog = new FrmSettingsDialog(_settings, _audioManager);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            if (_cmbDifficulty.Items.Contains(_settings.Difficulty))
            {
                _cmbDifficulty.SelectedItem = _settings.Difficulty;
            }

            RefreshContinueButtonState();
        }
    }

    private void HandleExitClick(object? sender, EventArgs e) => Close();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            for (int i = 0; i < _ownedFonts.Count; i++)
            {
                _ownedFonts[i].Dispose();
            }

            _ownedFonts.Clear();
        }
    }

    private Font CreateOwnedFont(string family, float size, FontStyle style = FontStyle.Regular)
    {
        var font = new Font(family, size, style);
        _ownedFonts.Add(font);
        return font;
    }

    private Button CreateMenuButton(string text, int topY, Color backColor)
    {
        return new Button
        {
            Text = text,
            Location = new Point(110, topY),
            Size = new Size(304, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = CreateOwnedFont("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }
}
