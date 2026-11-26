using System;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Audio;
using MiniNecesse.App.Settings;
using MiniNecesse.App.UI;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.World;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Settings dialog allowing configuration of audio volume, mute state, gameplay difficulty,
/// chunk view distance, and displaying active configurable key bindings.
/// </summary>
public sealed class FrmSettingsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly AudioManager _audioManager;
    private readonly CheckBox _chkMute;
    private readonly TrackBar _trkVolume;
    private readonly Label _lblVolumeValue;
    private readonly TrackBar _trkViewDistance;
    private readonly Label _lblViewDistanceValue;
    private readonly ComboBox _cmbDifficulty;
    private readonly TextBox _txtPlayerName;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly List<Font> _ownedFonts = new();

    /// <summary>Initializes a new instance of the <see cref="FrmSettingsDialog"/> class.</summary>
    public FrmSettingsDialog(AppSettings settings, AudioManager audioManager)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(audioManager);

        _settings = settings;
        _audioManager = audioManager;

        Text = "Settings — MiniNecesse";
        Size = new Size(465, 465);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UiTheme.BackgroundPrimary;
        ForeColor = UiTheme.TextPrimary;
        Font = CreateOwnedFont(UiTheme.FontFamily, 10f);

        var lblTitle = CreateLabel("GAME SETTINGS", 20, 16, 14f, FontStyle.Bold, UiTheme.AccentGold);
        Controls.Add(lblTitle);

        Controls.Add(CreateLabel("Player Name:", 24, 64));
        _txtPlayerName = new TextBox
        {
            Location = new Point(165, 60),
            Size = new Size(255, 28),
            BackColor = UiTheme.SurfaceInput,
            ForeColor = UiTheme.TextPrimary,
            Text = _settings.PlayerName
        };
        Controls.Add(_txtPlayerName);

        Controls.Add(CreateLabel("Difficulty:", 24, 106));
        _cmbDifficulty = new ComboBox
        {
            Location = new Point(165, 102),
            Size = new Size(255, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = UiTheme.SurfaceInput,
            ForeColor = UiTheme.TextPrimary
        };
        _cmbDifficulty.Items.AddRange(new object[] { Difficulty.Tutorial, Difficulty.Normal, Difficulty.Hard });
        _cmbDifficulty.SelectedItem = _cmbDifficulty.Items.Contains(_settings.Difficulty)
            ? _settings.Difficulty
            : Difficulty.Normal;
        Controls.Add(_cmbDifficulty);

        _chkMute = new CheckBox
        {
            Text = "Mute Audio",
            Location = new Point(24, 148),
            AutoSize = true,
            Checked = _settings.IsMuted
        };
        Controls.Add(_chkMute);

        Controls.Add(CreateLabel("Volume:", 24, 188));
        _trkVolume = new TrackBar
        {
            Location = new Point(160, 182),
            Size = new Size(195, 42),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = Math.Clamp(_settings.Volume, 0, 100)
        };
        _lblVolumeValue = CreateLabel($"{_trkVolume.Value}%", 362, 188);
        Controls.Add(_trkVolume);
        Controls.Add(_lblVolumeValue);

        Controls.Add(CreateLabel("View Distance:", 24, 240));
        _trkViewDistance = new TrackBar
        {
            Location = new Point(160, 234),
            Size = new Size(165, 42),
            Minimum = ChunkManager.DEFAULT_MIN_VIEW_DISTANCE,
            Maximum = ChunkManager.DEFAULT_MAX_VIEW_DISTANCE,
            TickFrequency = 1,
            Value = Math.Clamp(
                _settings.ViewDistanceChunks,
                ChunkManager.DEFAULT_MIN_VIEW_DISTANCE,
                ChunkManager.DEFAULT_MAX_VIEW_DISTANCE)
        };
        _lblViewDistanceValue = CreateLabel(FormatViewDistanceLabel(_trkViewDistance.Value), 330, 240, 9.2f, FontStyle.Bold, Color.FromArgb(118, 230, 146));
        Controls.Add(_trkViewDistance);
        Controls.Add(_lblViewDistanceValue);

        var lblViewHint = new Label
        {
            Text = "Chunk view radius: lower = higher FPS, higher = wider loaded area",
            Location = new Point(24, 282),
            Size = new Size(410, 22),
            ForeColor = Color.FromArgb(156, 174, 198),
            Font = CreateOwnedFont("Segoe UI", 8.5f, FontStyle.Italic)
        };
        Controls.Add(lblViewHint);

        string keysText = $"Controls: Move [{_settings.KeyBindings.MoveUp}{_settings.KeyBindings.MoveLeft}{_settings.KeyBindings.MoveDown}{_settings.KeyBindings.MoveRight}] | " +
                          $"Craft [{_settings.KeyBindings.ToggleCrafting}] | Boss/Use [{_settings.KeyBindings.Interact}] | Pause [{_settings.KeyBindings.Pause}] | Hotbar [1-9 / Wheel]";
        var lblControls = new Label
        {
            Text = keysText,
            Location = new Point(24, 314),
            Size = new Size(405, 46),
            ForeColor = Color.FromArgb(170, 182, 200),
            Font = CreateOwnedFont("Segoe UI", 8.5f)
        };
        Controls.Add(lblControls);

        _btnSave = CreateButton("Save", new Point(225, 376), UiTheme.AccentGreen);
        _btnCancel = CreateButton("Cancel", new Point(330, 376), UiTheme.ButtonNeutral);
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        _trkVolume.Scroll += HandleVolumeScroll;
        _trkViewDistance.Scroll += HandleViewDistanceScroll;
        _btnSave.Click += HandleSaveClick;
        _btnCancel.Click += HandleCancelClick;
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _trkVolume.Scroll -= HandleVolumeScroll;
        _trkViewDistance.Scroll -= HandleViewDistanceScroll;
        _btnSave.Click -= HandleSaveClick;
        _btnCancel.Click -= HandleCancelClick;
        base.OnFormClosing(e);
    }

    private void HandleVolumeScroll(object? sender, EventArgs e) => _lblVolumeValue.Text = $"{_trkVolume.Value}%";

    private void HandleViewDistanceScroll(object? sender, EventArgs e) => _lblViewDistanceValue.Text = FormatViewDistanceLabel(_trkViewDistance.Value);

    private void HandleSaveClick(object? sender, EventArgs e)
    {
        _settings.PlayerName = string.IsNullOrWhiteSpace(_txtPlayerName.Text) ? "Survivor" : _txtPlayerName.Text.Trim();
        _settings.Difficulty = _cmbDifficulty.SelectedItem is Difficulty diff ? diff : Difficulty.Normal;
        _settings.IsMuted = _chkMute.Checked;
        _settings.Volume = _trkVolume.Value;
        _settings.ViewDistanceChunks = _trkViewDistance.Value;
        _settings.Save();

        _audioManager.IsMuted = _settings.IsMuted;
        _audioManager.Volume = _settings.Volume;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void HandleCancelClick(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private static string FormatViewDistanceLabel(int radiusChunks)
    {
        int gridSide = (radiusChunks * 2) + 1;
        return $"{radiusChunks} ch ({gridSide}x{gridSide})";
    }

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

    private Label CreateLabel(string text, int x, int y, float size = 10f, FontStyle style = FontStyle.Regular, Color? color = null)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            Font = CreateOwnedFont("Segoe UI", size, style),
            ForeColor = color ?? Color.FromArgb(236, 240, 246)
        };
    }

    private static Button CreateButton(string text, Point location, Color backColor)
    {
        return new Button
        {
            Text = text,
            Location = location,
            Size = new Size(92, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
    }
}
