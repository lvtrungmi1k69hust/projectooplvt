using System;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Audio;
using MiniNecesse.App.Settings;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Settings dialog allowing configuration of audio volume, mute state, gameplay difficulty,
/// and displaying active configurable key bindings.
/// </summary>
public sealed class FrmSettingsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly AudioManager _audioManager;
    private readonly CheckBox _chkMute;
    private readonly TrackBar _trkVolume;
    private readonly Label _lblVolumeValue;
    private readonly ComboBox _cmbDifficulty;
    private readonly TextBox _txtPlayerName;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    /// <summary>Initializes a new instance of the <see cref="FrmSettingsDialog"/> class.</summary>
    public FrmSettingsDialog(AppSettings settings, AudioManager audioManager)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(audioManager);

        _settings = settings;
        _audioManager = audioManager;

        Text = "Settings — MiniNecesse";
        Size = new Size(440, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(24, 30, 42);
        ForeColor = Color.FromArgb(236, 240, 246);
        Font = new Font("Segoe UI", 10f);

        var lblTitle = CreateLabel("GAME SETTINGS", 20, 16, 14f, FontStyle.Bold, Color.FromArgb(240, 184, 77));
        Controls.Add(lblTitle);

        Controls.Add(CreateLabel("Player Name:", 24, 64));
        _txtPlayerName = new TextBox
        {
            Location = new Point(160, 60),
            Size = new Size(230, 28),
            BackColor = Color.FromArgb(35, 43, 60),
            ForeColor = Color.White,
            Text = _settings.PlayerName
        };
        Controls.Add(_txtPlayerName);

        Controls.Add(CreateLabel("Difficulty:", 24, 106));
        _cmbDifficulty = new ComboBox
        {
            Location = new Point(160, 102),
            Size = new Size(230, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(35, 43, 60),
            ForeColor = Color.White
        };
        _cmbDifficulty.Items.AddRange(new object[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard });
        _cmbDifficulty.SelectedItem = _settings.Difficulty;
        Controls.Add(_cmbDifficulty);

        _chkMute = new CheckBox
        {
            Text = "Mute Audio",
            Location = new Point(24, 150),
            AutoSize = true,
            Checked = _settings.IsMuted
        };
        Controls.Add(_chkMute);

        Controls.Add(CreateLabel("Volume:", 24, 190));
        _trkVolume = new TrackBar
        {
            Location = new Point(155, 184),
            Size = new Size(185, 42),
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = Math.Clamp(_settings.Volume, 0, 100)
        };
        _lblVolumeValue = CreateLabel($"{_trkVolume.Value}%", 346, 190);
        Controls.Add(_trkVolume);
        Controls.Add(_lblVolumeValue);

        string keysText = $"Controls: Move [{_settings.KeyBindings.MoveUp}{_settings.KeyBindings.MoveLeft}{_settings.KeyBindings.MoveDown}{_settings.KeyBindings.MoveRight}] | " +
                          $"Craft [{_settings.KeyBindings.ToggleCrafting}] | Boss/Use [{_settings.KeyBindings.Interact}] | Pause [{_settings.KeyBindings.Pause}] | Hotbar [1-9 / Wheel]";
        var lblControls = new Label
        {
            Text = keysText,
            Location = new Point(24, 240),
            Size = new Size(375, 46),
            ForeColor = Color.FromArgb(170, 182, 200),
            Font = new Font("Segoe UI", 8.5f)
        };
        Controls.Add(lblControls);

        _btnSave = CreateButton("Save", new Point(195, 302), Color.FromArgb(46, 156, 98));
        _btnCancel = CreateButton("Cancel", new Point(300, 302), Color.FromArgb(80, 90, 110));
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        _trkVolume.Scroll += HandleVolumeScroll;
        _btnSave.Click += HandleSaveClick;
        _btnCancel.Click += HandleCancelClick;
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _trkVolume.Scroll -= HandleVolumeScroll;
        _btnSave.Click -= HandleSaveClick;
        _btnCancel.Click -= HandleCancelClick;
        base.OnFormClosing(e);
    }

    private void HandleVolumeScroll(object? sender, EventArgs e) => _lblVolumeValue.Text = $"{_trkVolume.Value}%";

    private void HandleSaveClick(object? sender, EventArgs e)
    {
        _settings.PlayerName = string.IsNullOrWhiteSpace(_txtPlayerName.Text) ? "Survivor" : _txtPlayerName.Text.Trim();
        _settings.Difficulty = _cmbDifficulty.SelectedItem is Difficulty diff ? diff : Difficulty.Normal;
        _settings.IsMuted = _chkMute.Checked;
        _settings.Volume = _trkVolume.Value;

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

    private static Label CreateLabel(string text, int x, int y, float size = 10f, FontStyle style = FontStyle.Regular, Color? color = null)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            Font = new Font("Segoe UI", size, style),
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
