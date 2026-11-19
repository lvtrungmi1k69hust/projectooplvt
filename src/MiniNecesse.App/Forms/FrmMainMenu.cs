using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Audio;
using MiniNecesse.App.Settings;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Main Menu presentation screen providing New Game (Island 1-5 selector),
/// Continue (Load Save JSON), High Scores, Settings, and Exit via Constructor DI.
/// </summary>
public sealed class FrmMainMenu : Form
{
    private readonly ILevelLoader _levelLoader;
    private readonly ISaveStorage _saveStorage;
    private readonly IHighScoreRepository _highScoreRepository;
    private readonly AudioManager _audioManager;
    private readonly AppSettings _settings;
    private readonly Func<string, bool, Form>? _gameplayLauncher;

    private readonly ComboBox _cmbIslandSelector;
    private readonly Button _btnNewGame;
    private readonly Button _btnContinue;
    private readonly Button _btnHighScores;
    private readonly Button _btnSettings;
    private readonly Button _btnExit;
    private readonly Label _lblStatus;

    /// <summary>Initializes a new instance of the <see cref="FrmMainMenu"/> class via Constructor DI.</summary>
    public FrmMainMenu(
        ILevelLoader levelLoader,
        ISaveStorage saveStorage,
        IHighScoreRepository highScoreRepository,
        AudioManager audioManager,
        AppSettings settings,
        Func<string, bool, Form>? gameplayLauncher = null)
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
        Size = new Size(540, 560);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(18, 24, 34);
        ForeColor = Color.FromArgb(236, 240, 246);
        Font = new Font("Segoe UI", 10.5f);

        Controls.Add(new Label
        {
            Text = "MININECESSE",
            Location = new Point(0, 32),
            Size = new Size(524, 46),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 26f, FontStyle.Bold),
            ForeColor = Color.FromArgb(242, 190, 84)
        });

        Controls.Add(new Label
        {
            Text = "2D Top-Down Survival • Crafting • Island Bosses",
            Location = new Point(0, 82),
            Size = new Size(524, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10f, FontStyle.Italic),
            ForeColor = Color.FromArgb(156, 174, 198)
        });

        Controls.Add(new Label
        {
            Text = "Select Starting Island:",
            Location = new Point(110, 128),
            AutoSize = true,
            ForeColor = Color.FromArgb(204, 214, 228)
        });

        _cmbIslandSelector = new ComboBox
        {
            Location = new Point(110, 154),
            Size = new Size(304, 30),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(30, 39, 54),
            ForeColor = Color.White
        };
        Controls.Add(_cmbIslandSelector);

        _btnNewGame = CreateMenuButton("Play New Game", 204, Color.FromArgb(46, 156, 98));
        _btnContinue = CreateMenuButton("Continue (Load Save Slot 1)", 258, Color.FromArgb(48, 118, 190));
        _btnHighScores = CreateMenuButton("High Scores (Top 10)", 312, Color.FromArgb(156, 112, 42));
        _btnSettings = CreateMenuButton("Settings (Audio & Difficulty)", 366, Color.FromArgb(74, 88, 114));
        _btnExit = CreateMenuButton("Exit", 420, Color.FromArgb(164, 58, 58));

        _lblStatus = new Label
        {
            Location = new Point(20, 480),
            Size = new Size(484, 26),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(146, 166, 190)
        };

        Controls.AddRange(new Control[]
        {
            _btnNewGame, _btnContinue, _btnHighScores, _btnSettings, _btnExit, _lblStatus
        });

        LoadAvailableIslands();
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
        _btnNewGame.Click += HandleNewGameClick;
        _btnContinue.Click += HandleContinueClick;
        _btnHighScores.Click += HandleHighScoresClick;
        _btnSettings.Click += HandleSettingsClick;
        _btnExit.Click += HandleExitClick;
    }

    private void UnsubscribeEvents()
    {
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

    private void RefreshContinueButtonState()
    {
        bool hasSave = _saveStorage.Exists(1);
        _btnContinue.Enabled = hasSave;
        _lblStatus.Text = $"Difficulty: {_settings.Difficulty} | Audio: {(_settings.IsMuted ? "Muted" : $"{_settings.Volume}%")} | Save Slot 1: {(hasSave ? "Available" : "Empty")}";
    }

    private void HandleNewGameClick(object? sender, EventArgs e)
    {
        string selectedIsland = _cmbIslandSelector.SelectedItem?.ToString() ?? "ISLAND_01";
        LaunchGameplay(selectedIsland, loadSavedSlot: false);
    }

    private void HandleContinueClick(object? sender, EventArgs e)
    {
        LaunchGameplay("ISLAND_01", loadSavedSlot: true);
    }

    private void LaunchGameplay(string islandId, bool loadSavedSlot)
    {
        if (_gameplayLauncher is null)
        {
            MessageBox.Show(this, "Gameplay view is not configured yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Hide();
        try
        {
            using Form gameForm = _gameplayLauncher(islandId, loadSavedSlot);
            gameForm.ShowDialog(this);
        }
        finally
        {
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
            RefreshContinueButtonState();
        }
    }

    private void HandleExitClick(object? sender, EventArgs e) => Close();

    private static Button CreateMenuButton(string text, int topY, Color backColor)
    {
        return new Button
        {
            Text = text,
            Location = new Point(110, topY),
            Size = new Size(304, 42),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }
}
