using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Rendering;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Settlement;

namespace MiniNecesse.App.Views;

/// <summary>
/// Interactive Colony &amp; Settler management overlay displaying detected valid houses,
/// JSON recruitment options (Miner / Hunter), settler happiness, produced stockpile collection,
/// and barter trading with strict GDI+ Font and child Control disposal.
/// </summary>
public sealed class SettlerOverlayPanel : Panel
{
    private readonly Label _lblSummary;
    private readonly Label _lblSettlerInfo;
    private readonly FlowLayoutPanel _recruitPanel;
    private readonly FlowLayoutPanel _tradePanel;
    private readonly Button _btnCollect;
    private readonly Button _btnClose;
    private readonly List<Button> _dynamicButtons = new();
    private readonly Font _headerFont = new("Segoe UI", 12f, FontStyle.Bold);
    private readonly Font _subFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font _rowFont = new("Segoe UI", 8.8f);

    private GameSession? _session;
    private Settler? _focusedSettler;
    private SpriteAtlasCache? _sprites;

    /// <summary>Raised when the user closes this overlay.</summary>
    public event Action? OnCloseRequested;

    /// <summary>Initializes a new instance of the <see cref="SettlerOverlayPanel"/> class.</summary>
    public SettlerOverlayPanel()
    {
        Size = new Size(700, 500);
        BackColor = Color.FromArgb(20, 26, 38);
        BorderStyle = BorderStyle.FixedSingle;
        Visible = false;

        Controls.Add(new Label
        {
            Text = "COLONY & SETTLERS  (Press V or Esc to close)",
            UseMnemonic = false,
            Location = new Point(16, 12),
            Size = new Size(668, 26),
            Font = _headerFont,
            ForeColor = Color.FromArgb(246, 196, 82)
        });

        _lblSummary = new Label
        {
            UseMnemonic = false,
            Location = new Point(16, 42),
            Size = new Size(668, 22),
            Font = _subFont,
            ForeColor = Color.FromArgb(146, 226, 152)
        };
        Controls.Add(_lblSummary);

        Controls.Add(new Label
        {
            Text = "Recruit Settlers (From settlers.json)",
            Location = new Point(16, 70),
            Size = new Size(310, 20),
            Font = _subFont,
            ForeColor = Color.FromArgb(224, 232, 244)
        });

        _recruitPanel = CreateListPanel(16, 94, 668, 112);
        Controls.Add(_recruitPanel);

        _lblSettlerInfo = new Label
        {
            UseMnemonic = false,
            Location = new Point(16, 214),
            Size = new Size(512, 24),
            Font = _subFont,
            ForeColor = Color.FromArgb(146, 210, 246)
        };
        Controls.Add(_lblSettlerInfo);

        _btnCollect = new Button
        {
            Text = "Collect Stash",
            Location = new Point(538, 210),
            Size = new Size(146, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(46, 146, 96),
            ForeColor = Color.White,
            Font = _rowFont,
            Cursor = Cursors.Hand
        };
        _btnCollect.Click += HandleCollectClick;
        Controls.Add(_btnCollect);

        _tradePanel = CreateListPanel(16, 246, 668, 200);
        Controls.Add(_tradePanel);

        _btnClose = new Button
        {
            Text = "Close (V)",
            Location = new Point(576, 456),
            Size = new Size(108, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(78, 92, 118),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _btnClose.Click += HandleCloseClick;
        Controls.Add(_btnClose);
    }

    /// <summary>Binds this panel to the active session and optional nearby settler.</summary>
    public void Bind(GameSession session, Settler? focusedSettler, SpriteAtlasCache? sprites)
    {
        _session = session;
        _focusedSettler = focusedSettler;
        _sprites = sprites;
        RefreshView();
    }

    /// <summary>Rebuilds recruitment and trade rows from current Core settlement state.</summary>
    public void RefreshView()
    {
        if (_session is null) return;

        SettlementManager settlement = _session.Settlement;
        settlement.RefreshHousing(_session.Level, _session.Structures, _session.FindStructureAtTile, _session.Enemies);

        _lblSummary.Text = $"Valid Enclosed Houses: {settlement.ValidHouses.Count}  |  Active Settlers: {settlement.Settlers.Count}";

        if (_focusedSettler is null || !_focusedSettler.IsAlive)
        {
            _focusedSettler = settlement.Settlers.Count > 0 ? settlement.Settlers[0] : null;
        }

        _recruitPanel.SuspendLayout();
        _tradePanel.SuspendLayout();
        ClearDynamicRows();

        PopulateRecruitRows(settlement);
        PopulateFocusedSettlerSection();

        _recruitPanel.ResumeLayout();
        _tradePanel.ResumeLayout();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _btnCollect.Click -= HandleCollectClick;
            _btnClose.Click -= HandleCloseClick;
            ClearDynamicRows();
            _headerFont.Dispose();
            _subFont.Dispose();
            _rowFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PopulateRecruitRows(SettlementManager settlement)
    {
        IReadOnlyList<SettlerProfessionDefinition> profs = settlement.Catalog.Professions;
        for (int i = 0; i < profs.Count; i++)
        {
            SettlerProfessionDefinition def = profs[i];
            bool canAfford = _session!.Inventory.HasItem(def.RecruitCostItemId, def.RecruitCostQuantity);

            var row = new Panel
            {
                Size = new Size(640, 44),
                Margin = new Padding(3, 2, 3, 2),
                BackColor = canAfford ? Color.FromArgb(32, 68, 48) : Color.FromArgb(64, 34, 38)
            };

            string iconKey = def.Profession == Core.Enums.SettlerProfession.Hunter ? "settler_hunter" : "settler_miner";
            Bitmap? icon = _sprites?.Get(iconKey);
            if (icon is not null)
            {
                row.Controls.Add(new PictureBox
                {
                    Image = icon,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Location = new Point(8, 8),
                    Size = new Size(28, 28),
                    BackColor = Color.Transparent
                });
            }

            row.Controls.Add(new Label
            {
                Text = $"{def.DisplayName} — Cost: {def.RecruitCostQuantity}x {def.RecruitCostItemId}  (Produces every {def.WorkIntervalSeconds:0}s)",
                UseMnemonic = false,
                Location = new Point(44, 12),
                Size = new Size(470, 22),
                Font = _rowFont,
                ForeColor = Color.White
            });

            var btnRecruit = new Button
            {
                Text = "Recruit",
                Location = new Point(536, 7),
                Size = new Size(92, 30),
                Enabled = canAfford,
                Tag = def.ProfessionId,
                FlatStyle = FlatStyle.Flat,
                BackColor = canAfford ? Color.FromArgb(46, 158, 96) : Color.FromArgb(84, 60, 64),
                ForeColor = Color.White,
                Font = _rowFont,
                Cursor = Cursors.Hand
            };
            btnRecruit.Click += HandleRecruitClick;
            _dynamicButtons.Add(btnRecruit);
            row.Controls.Add(btnRecruit);
            _recruitPanel.Controls.Add(row);
        }
    }

    private void PopulateFocusedSettlerSection()
    {
        if (_focusedSettler is null || _session is null)
        {
            _lblSettlerInfo.Text = "No settler recruited yet — recruit above or walk near a settler [E].";
            _btnCollect.Enabled = false;
            _tradePanel.Controls.Add(new Label
            {
                Text = "Build an enclosed room (walls + Wooden Door) for +40 happiness (+15 with Torch) to unlock trade discounts!",
                Location = new Point(8, 10),
                Size = new Size(630, 36),
                Font = _rowFont,
                ForeColor = Color.FromArgb(164, 180, 204)
            });
            return;
        }

        string homeTag = _focusedSettler.HasValidHome ? "Housed" : "Homeless";
        int stashUnits = CountStashUnits(_focusedSettler.Stockpile);
        _lblSettlerInfo.Text = $"{_focusedSettler.Name} ({_focusedSettler.Profession}) | {_focusedSettler.State} | Happy: {_focusedSettler.Happiness}% ({homeTag}) | Stash: {stashUnits}";
        _btnCollect.Enabled = stashUnits > 0;
        _btnCollect.Text = stashUnits > 0 ? $"Collect ({stashUnits})" : "Stash Empty";

        IReadOnlyList<SettlerTradeOffer> offers = _focusedSettler.Definition.Trades;
        for (int i = 0; i < offers.Count; i++)
        {
            SettlerTradeOffer offer = offers[i];
            int effectiveCost = _focusedSettler.GetEffectiveTradeCost(offer.CostQuantity);
            bool canTrade = _session.Inventory.HasItem(offer.CostItemId, effectiveCost);

            var row = new Panel
            {
                Size = new Size(640, 44),
                Margin = new Padding(3, 2, 3, 2),
                BackColor = canTrade ? Color.FromArgb(34, 52, 74) : Color.FromArgb(46, 40, 52)
            };

            row.Controls.Add(new Label
            {
                Text = $"Trade: {effectiveCost}x {offer.CostItemId}  ->  {offer.RewardQuantity}x {offer.RewardItemId}  (Base: {offer.CostQuantity})",
                UseMnemonic = false,
                Location = new Point(12, 12),
                Size = new Size(500, 22),
                Font = _rowFont,
                ForeColor = Color.White
            });

            var btnTrade = new Button
            {
                Text = "Trade",
                Location = new Point(536, 7),
                Size = new Size(92, 30),
                Enabled = canTrade,
                Tag = offer.OfferId,
                FlatStyle = FlatStyle.Flat,
                BackColor = canTrade ? Color.FromArgb(52, 124, 196) : Color.FromArgb(72, 76, 92),
                ForeColor = Color.White,
                Font = _rowFont,
                Cursor = Cursors.Hand
            };
            btnTrade.Click += HandleTradeClick;
            _dynamicButtons.Add(btnTrade);
            row.Controls.Add(btnTrade);
            _tradePanel.Controls.Add(row);
        }
    }

    private static int CountStashUnits(Inventory stockpile)
    {
        int total = 0;
        for (int i = 0; i < stockpile.Stacks.Count; i++)
        {
            total += stockpile.Stacks[i].Quantity;
        }

        return total;
    }

    private static FlowLayoutPanel CreateListPanel(int x, int y, int width, int height)
    {
        var panel = new FlowLayoutPanel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(26, 33, 48)
        };
        panel.HorizontalScroll.Maximum = 0;
        panel.HorizontalScroll.Visible = false;
        return panel;
    }

    private void ClearDynamicRows()
    {
        for (int i = 0; i < _dynamicButtons.Count; i++)
        {
            _dynamicButtons[i].Click -= HandleRecruitClick;
            _dynamicButtons[i].Click -= HandleTradeClick;
        }

        _dynamicButtons.Clear();
        DisposePanelChildren(_recruitPanel);
        DisposePanelChildren(_tradePanel);
    }

    private static void DisposePanelChildren(FlowLayoutPanel panel)
    {
        if (panel.Controls.Count == 0) return;
        var removed = new Control[panel.Controls.Count];
        panel.Controls.CopyTo(removed, 0);
        panel.Controls.Clear();

        for (int i = 0; i < removed.Length; i++)
        {
            Control c = removed[i];
            for (int j = 0; j < c.Controls.Count; j++)
            {
                if (c.Controls[j] is PictureBox pb)
                {
                    pb.Image = null;
                }
            }

            c.Dispose();
        }
    }

    private void HandleRecruitClick(object? sender, EventArgs e)
    {
        if (_session is null || sender is not Button { Tag: string professionId })
        {
            return;
        }

        if (_session.TryRecruitSettler(professionId, out Settler? recruited) && recruited is not null)
        {
            _focusedSettler = recruited;
            RefreshView();
        }
    }

    private void HandleTradeClick(object? sender, EventArgs e)
    {
        if (_session is null || _focusedSettler is null || sender is not Button { Tag: string offerId })
        {
            return;
        }

        if (_session.Settlement.TryTrade(_focusedSettler, offerId, _session.Player))
        {
            RefreshView();
        }
    }

    private void HandleCollectClick(object? sender, EventArgs e)
    {
        if (_session is null || _focusedSettler is null)
        {
            return;
        }

        if (_session.Settlement.TryCollectProducedItems(_focusedSettler, _session.Player) > 0)
        {
            RefreshView();
        }
    }

    private void HandleCloseClick(object? sender, EventArgs e) => OnCloseRequested?.Invoke();
}
