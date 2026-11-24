using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MiniNecesse.App.Rendering;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Views;

/// <summary>
/// Interactive Crafting &amp; Inventory overlay panel toggled with key <c>C</c> or <c>Tab</c>.
/// Strictly delegates recipe validation to <see cref="CraftingSystem.CanCraft"/> and crafting execution
/// to <see cref="GameSession.TryCraft"/> without duplicating domain rules in UI.
/// </summary>
public sealed class CraftingOverlayPanel : Panel
{
    private readonly Label _lblWorkbenchStatus;
    private readonly Label _lblInventorySummary;
    private readonly FlowLayoutPanel _recipeListPanel;
    private readonly Button _btnClose;
    private readonly List<Button> _dynamicCraftButtons = new();

    private GameSession? _session;
    private IReadOnlyList<CraftingRecipe> _recipes = Array.Empty<CraftingRecipe>();
    private SpriteAtlasCache? _sprites;
    private float _workbenchRadius = 80f;

    /// <summary>Raised when the user clicks the Close button.</summary>
    public event Action? OnCloseRequested;

    /// <summary>Initializes a new instance of the <see cref="CraftingOverlayPanel"/> class.</summary>
    public CraftingOverlayPanel()
    {
        Size = new Size(660, 510);
        BackColor = Color.FromArgb(20, 26, 38);
        BorderStyle = BorderStyle.FixedSingle;
        Visible = false;

        var lblHeader = new Label
        {
            Text = "INVENTORY & CRAFTING  (Press C or Tab to close)",
            UseMnemonic = false,
            Location = new Point(16, 12),
            Size = new Size(624, 28),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(246, 196, 82)
        };
        Controls.Add(lblHeader);

        _lblWorkbenchStatus = new Label
        {
            UseMnemonic = false,
            Location = new Point(16, 44),
            Size = new Size(624, 24),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 226, 152)
        };
        Controls.Add(_lblWorkbenchStatus);

        _lblInventorySummary = new Label
        {
            UseMnemonic = false,
            Location = new Point(16, 70),
            Size = new Size(624, 46),
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = Color.FromArgb(212, 222, 236)
        };
        Controls.Add(_lblInventorySummary);

        _recipeListPanel = new FlowLayoutPanel
        {
            Location = new Point(16, 120),
            Size = new Size(626, 330),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.FromArgb(26, 33, 48)
        };
        _recipeListPanel.HorizontalScroll.Maximum = 0;
        _recipeListPanel.HorizontalScroll.Visible = false;
        Controls.Add(_recipeListPanel);

        _btnClose = new Button
        {
            Text = "Close (C)",
            Location = new Point(536, 462),
            Size = new Size(106, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(78, 92, 118),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _btnClose.Click += HandleCloseClick;
        Controls.Add(_btnClose);
    }

    /// <summary>Binds this panel to the active <see cref="GameSession"/>, recipe catalog, and sprite atlas.</summary>
    public void Bind(GameSession session, IReadOnlyList<CraftingRecipe> recipes, float workbenchRadius, SpriteAtlasCache? sprites = null)
    {
        _session = session;
        _recipes = recipes;
        _workbenchRadius = workbenchRadius;
        _sprites = sprites;
        RefreshView();
    }

    /// <summary>Rebuilds recipe rows with green/red craftability highlighting from Core rules.</summary>
    public void RefreshView()
    {
        if (_session is null) return;

        bool nearWorkbench = _session.IsPlayerNearWorkbench(_workbenchRadius);
        _lblWorkbenchStatus.Text = nearWorkbench
            ? "Workbench Nearby: YES — All [WB] recipes unlocked!"
            : "Workbench Nearby: NO — Craft a Workbench (6 Wood) and place it [RMB] nearby.";
        _lblWorkbenchStatus.ForeColor = nearWorkbench
            ? Color.FromArgb(118, 232, 146)
            : Color.FromArgb(244, 162, 102);

        _lblInventorySummary.Text = BuildInventorySummaryText(_session);

        _recipeListPanel.SuspendLayout();
        ClearRecipeRows();
        for (int i = 0; i < _recipes.Count; i++)
        {
            CraftingRecipe recipe = _recipes[i];
            bool canCraft = _session.CraftingSystem.CanCraft(_session.Inventory, recipe, nearWorkbench);
            _recipeListPanel.Controls.Add(CreateRecipeRow(recipe, canCraft));
        }

        _recipeListPanel.ResumeLayout();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _btnClose.Click -= HandleCloseClick;
            ClearRecipeRows();
        }

        base.Dispose(disposing);
    }

    private Panel CreateRecipeRow(CraftingRecipe recipe, bool canCraft)
    {
        var row = new Panel
        {
            Size = new Size(588, 60),
            Margin = new Padding(4, 3, 4, 3),
            BackColor = canCraft ? Color.FromArgb(32, 72, 48) : Color.FromArgb(68, 34, 38)
        };

        string ingText = string.Join("  |  ", recipe.Ingredients.Select(ing =>
        {
            int owned = _session?.Inventory.GetItemCount(ing.ItemId) ?? 0;
            return $"{FormatDisplayName(ing.ItemId)} {owned}/{ing.Quantity}";
        }));
        string wbTag = recipe.RequiresWorkbench ? " [Requires Workbench]" : " [Hand Craft]";

        Bitmap? icon = _sprites?.Get(recipe.OutputItemId);
        if (icon is not null)
        {
            row.Controls.Add(new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(10, 12),
                Size = new Size(36, 36),
                BackColor = Color.Transparent
            });
        }

        int textLeft = icon is not null ? 56 : 12;
        row.Controls.Add(new Label
        {
            Text = $"{FormatDisplayName(recipe.OutputItemId)} x{recipe.OutputQuantity}{wbTag}",
            UseMnemonic = false,
            Location = new Point(textLeft, 6),
            Size = new Size(424, 24),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = canCraft ? Color.FromArgb(174, 248, 188) : Color.FromArgb(246, 174, 174)
        });

        row.Controls.Add(new Label
        {
            Text = $"Materials: {ingText}",
            UseMnemonic = false,
            Location = new Point(textLeft, 32),
            Size = new Size(424, 22),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(224, 230, 240)
        });

        var btnCraft = new Button
        {
            Text = "Craft",
            Location = new Point(490, 14),
            Size = new Size(86, 32),
            Enabled = canCraft,
            Tag = recipe,
            FlatStyle = FlatStyle.Flat,
            BackColor = canCraft ? Color.FromArgb(48, 166, 98) : Color.FromArgb(90, 62, 66),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnCraft.Click += HandleCraftButtonClick;
        _dynamicCraftButtons.Add(btnCraft);
        row.Controls.Add(btnCraft);

        return row;
    }

    private void ClearRecipeRows()
    {
        for (int i = 0; i < _dynamicCraftButtons.Count; i++)
        {
            _dynamicCraftButtons[i].Click -= HandleCraftButtonClick;
        }

        _dynamicCraftButtons.Clear();
        _recipeListPanel.Controls.Clear();
    }

    private void HandleCraftButtonClick(object? sender, EventArgs e)
    {
        if (_session is null || sender is not Button { Tag: CraftingRecipe recipe })
        {
            return;
        }

        if (_session.TryCraft(recipe, _workbenchRadius))
        {
            RefreshView();
        }
    }

    private void HandleCloseClick(object? sender, EventArgs e) => OnCloseRequested?.Invoke();

    private static string BuildInventorySummaryText(GameSession session)
    {
        if (session.Inventory.Stacks.Count == 0)
        {
            return "Bag: (empty)";
        }

        string items = string.Join(" • ", session.Inventory.Stacks.Select(s => $"{s.Item.Name} x{s.Quantity}"));
        return $"Bag ({session.Inventory.UsedSlots}/{session.Inventory.Capacity}): {items}";
    }

    private static string FormatDisplayName(string itemId)
    {
        string[] parts = itemId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        }

        return string.Join(' ', parts);
    }
}
