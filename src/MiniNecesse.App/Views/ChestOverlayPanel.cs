using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Rendering;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Views;

/// <summary>
/// Interactive storage chest overlay displaying Player Inventory alongside Chest Storage
/// with proper GDI+ Font and child Control disposal to avoid handle leaks.
/// </summary>
public sealed class ChestOverlayPanel : Panel
{
    private sealed class TransferAction
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public bool Deposit { get; }

        public TransferAction(string itemId, int quantity, bool deposit)
        {
            ItemId = itemId;
            Quantity = quantity;
            Deposit = deposit;
        }
    }

    private readonly Label _lblTitle;
    private readonly FlowLayoutPanel _playerList;
    private readonly FlowLayoutPanel _chestList;
    private readonly Button _btnClose;
    private readonly List<Button> _dynamicButtons = new();
    private readonly Font _headerFont = new("Segoe UI", 12f, FontStyle.Bold);
    private readonly Font _subFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font _rowFont = new("Segoe UI", 8.8f);

    private GameSession? _session;
    private Structure? _chest;
    private SpriteAtlasCache? _sprites;

    /// <summary>Raised when the user requests closing the chest overlay.</summary>
    public event Action? OnCloseRequested;

    /// <summary>Initializes a new instance of the <see cref="ChestOverlayPanel"/> class.</summary>
    public ChestOverlayPanel()
    {
        Size = new Size(700, 480);
        BackColor = Color.FromArgb(20, 26, 38);
        BorderStyle = BorderStyle.FixedSingle;
        Visible = false;

        _lblTitle = new Label
        {
            Text = "STORAGE CHEST  (Press Esc or Close to return)",
            UseMnemonic = false,
            Location = new Point(16, 12),
            Size = new Size(668, 28),
            Font = _headerFont,
            ForeColor = Color.FromArgb(246, 196, 82)
        };
        Controls.Add(_lblTitle);

        Controls.Add(new Label
        {
            Text = "Player Bag (Click to Store)",
            Location = new Point(16, 46),
            Size = new Size(320, 22),
            Font = _subFont,
            ForeColor = Color.FromArgb(146, 226, 152)
        });

        Controls.Add(new Label
        {
            Text = "Chest Inventory (Click to Take)",
            Location = new Point(360, 46),
            Size = new Size(320, 22),
            Font = _subFont,
            ForeColor = Color.FromArgb(146, 210, 246)
        });

        _playerList = CreateColumnList(16, 72);
        _chestList = CreateColumnList(360, 72);
        Controls.Add(_playerList);
        Controls.Add(_chestList);

        _btnClose = new Button
        {
            Text = "Close (Esc)",
            Location = new Point(576, 434),
            Size = new Size(106, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(78, 92, 118),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _btnClose.Click += HandleCloseClick;
        Controls.Add(_btnClose);
    }

    /// <summary>Binds this overlay to a session and chest structure and refreshes rows.</summary>
    public void BindChest(GameSession session, Structure chest, SpriteAtlasCache? sprites)
    {
        _session = session;
        _chest = chest;
        _sprites = sprites;
        RefreshView();
    }

    /// <summary>Rebuilds player and chest item rows safely.</summary>
    public void RefreshView()
    {
        if (_session is null || _chest is null || _chest.StorageInventory is null)
        {
            return;
        }

        _lblTitle.Text = $"WOODEN CHEST ({_chest.StorageInventory.UsedSlots}/{_chest.StorageInventory.Capacity} slots)  |  DEF: {_session.Player.TotalDefense}";

        _playerList.SuspendLayout();
        _chestList.SuspendLayout();
        ClearAllRows();

        PopulateStackRows(_playerList, _session.Inventory.Stacks, deposit: true);
        PopulateStackRows(_chestList, _chest.StorageInventory.Stacks, deposit: false);

        _playerList.ResumeLayout();
        _chestList.ResumeLayout();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _btnClose.Click -= HandleCloseClick;
            ClearAllRows();
            _headerFont.Dispose();
            _subFont.Dispose();
            _rowFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private static FlowLayoutPanel CreateColumnList(int x, int y)
    {
        var panel = new FlowLayoutPanel
        {
            Location = new Point(x, y),
            Size = new Size(322, 350),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(26, 33, 48)
        };
        panel.HorizontalScroll.Maximum = 0;
        panel.HorizontalScroll.Visible = false;
        return panel;
    }

    private void PopulateStackRows(FlowLayoutPanel panel, IReadOnlyList<ItemStack> stacks, bool deposit)
    {
        if (stacks.Count == 0)
        {
            panel.Controls.Add(new Label
            {
                Text = "(Empty)",
                Location = new Point(8, 8),
                Size = new Size(280, 28),
                Font = _rowFont,
                ForeColor = Color.FromArgb(148, 162, 184)
            });
            return;
        }

        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            panel.Controls.Add(CreateItemRow(stack, deposit));
        }
    }

    private Panel CreateItemRow(ItemStack stack, bool deposit)
    {
        var row = new Panel
        {
            Size = new Size(298, 44),
            Margin = new Padding(3, 2, 3, 2),
            BackColor = Color.FromArgb(34, 44, 62)
        };

        Bitmap? icon = _sprites?.Get(stack.Item.Id);
        if (icon is not null)
        {
            row.Controls.Add(new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(6, 8),
                Size = new Size(28, 28),
                BackColor = Color.Transparent
            });
        }

        row.Controls.Add(new Label
        {
            Text = $"{stack.Item.Name} x{stack.Quantity}",
            UseMnemonic = false,
            Location = new Point(icon is not null ? 40 : 8, 12),
            Size = new Size(146, 22),
            Font = _rowFont,
            ForeColor = Color.White
        });

        Button btnOne = MakeTransferButton(deposit ? "+1" : "-1", 190, new TransferAction(stack.Item.Id, 1, deposit));
        Button btnAll = MakeTransferButton("All", 242, new TransferAction(stack.Item.Id, stack.Quantity, deposit));
        row.Controls.Add(btnOne);
        row.Controls.Add(btnAll);
        return row;
    }

    private Button MakeTransferButton(string text, int x, TransferAction action)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, 8),
            Size = new Size(48, 28),
            Tag = action,
            FlatStyle = FlatStyle.Flat,
            BackColor = action.Deposit ? Color.FromArgb(46, 134, 88) : Color.FromArgb(48, 112, 178),
            ForeColor = Color.White,
            Font = _rowFont,
            Cursor = Cursors.Hand
        };
        btn.Click += HandleTransferButtonClick;
        _dynamicButtons.Add(btn);
        return btn;
    }

    private void ClearAllRows()
    {
        for (int i = 0; i < _dynamicButtons.Count; i++)
        {
            _dynamicButtons[i].Click -= HandleTransferButtonClick;
        }

        _dynamicButtons.Clear();
        DisposePanelChildren(_playerList);
        DisposePanelChildren(_chestList);
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

    private void HandleTransferButtonClick(object? sender, EventArgs e)
    {
        if (_session is null || _chest is null || sender is not Button { Tag: TransferAction action })
        {
            return;
        }

        bool ok = action.Deposit
            ? _session.TryDepositToChest(_chest, action.ItemId, action.Quantity)
            : _session.TryWithdrawFromChest(_chest, action.ItemId, action.Quantity);

        if (ok)
        {
            RefreshView();
        }
    }

    private void HandleCloseClick(object? sender, EventArgs e) => OnCloseRequested?.Invoke();
}
