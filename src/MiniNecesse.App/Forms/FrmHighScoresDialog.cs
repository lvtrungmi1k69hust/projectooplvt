using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Read-only leaderboard dialog displaying Top 10 high scores loaded via <see cref="IHighScoreRepository"/>.
/// </summary>
public sealed class FrmHighScoresDialog : Form
{
    private readonly ListView _listScores;
    private readonly Button _btnClose;
    private readonly List<Font> _ownedFonts = new();

    /// <summary>Initializes a new instance of the <see cref="FrmHighScoresDialog"/> class.</summary>
    public FrmHighScoresDialog(IHighScoreRepository highScoreRepository)
    {
        ArgumentNullException.ThrowIfNull(highScoreRepository);

        Text = "High Scores — Top 10 Survivors";
        Size = new Size(560, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(24, 30, 42);
        ForeColor = Color.FromArgb(236, 240, 246);
        Font = CreateOwnedFont("Segoe UI", 10f);

        var lblTitle = new Label
        {
            Text = "TOP 10 HIGH SCORES",
            Location = new Point(20, 16),
            AutoSize = true,
            Font = CreateOwnedFont("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(240, 184, 77)
        };
        Controls.Add(lblTitle);

        _listScores = new ListView
        {
            Location = new Point(20, 56),
            Size = new Size(504, 265),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.FromArgb(32, 40, 56),
            ForeColor = Color.White
        };
        _listScores.Columns.Add("#", 38);
        _listScores.Columns.Add("Player", 145);
        _listScores.Columns.Add("Score", 85);
        _listScores.Columns.Add("Island", 95);
        _listScores.Columns.Add("Difficulty", 80);
        _listScores.Columns.Add("Date", 110);
        Controls.Add(_listScores);

        _btnClose = new Button
        {
            Text = "Close",
            Location = new Point(424, 334),
            Size = new Size(100, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(72, 86, 112),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _btnClose.Click += HandleCloseClick;
        Controls.Add(_btnClose);

        PopulateScores(highScoreRepository);
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _btnClose.Click -= HandleCloseClick;
        base.OnFormClosing(e);
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

    private void PopulateScores(IHighScoreRepository repository)
    {
        try
        {
            IReadOnlyList<HighScoreEntry> entries = repository.LoadTopScores();
            for (int i = 0; i < entries.Count; i++)
            {
                HighScoreEntry entry = entries[i];
                var row = new ListViewItem((i + 1).ToString());
                row.SubItems.Add(entry.PlayerName);
                row.SubItems.Add(entry.Score.ToString());
                row.SubItems.Add(entry.IslandId);
                row.SubItems.Add(entry.Difficulty);
                row.SubItems.Add(entry.AchievedAtUtc.ToLocalTime().ToString("yyyy-MM-dd"));
                _listScores.Items.Add(row);
            }

            if (entries.Count == 0)
            {
                var emptyRow = new ListViewItem("-");
                emptyRow.SubItems.Add("No scores recorded yet");
                _listScores.Items.Add(emptyRow);
            }
        }
        catch (SaveCorruptedException ex)
        {
            MessageBox.Show(this, $"Could not load high scores: {ex.Message}", "Data Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void HandleCloseClick(object? sender, EventArgs e) => Close();
}
