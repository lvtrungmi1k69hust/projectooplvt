using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using MiniNecesse.App.Rendering;
using MiniNecesse.App.UI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Views;

/// <summary>
/// Interactive Crafting, Unlimited Backpack &amp; 9-Slot Hotbar Replacement overlay panel toggled with <c>C</c> or <c>Tab</c>.
/// Delegates all recipe validation, crafting, armor equipping, and hotbar slot rules (1–3 Weapons-only, 4–9 Any)
/// directly to Core domain objects without duplicating rules in UI.
/// </summary>
public sealed class CraftingOverlayPanel : Panel
{
    private enum PanelTab
    {
        Recipes = 0,
        Backpack = 1
    }

    private const int HOTBAR_SLOTS = 9;
    private static readonly string[] HOTBAR_SLOT_BADGES = { "1W", "2W", "3W", "4", "5", "6", "7", "8", "9" };

    private readonly Label _lblWorkbenchStatus;
    private readonly Label _lblInventorySummary;
    private readonly Button _btnTabRecipes;
    private readonly Button _btnTabBackpack;
    private readonly Label _lblFeedback;
    private readonly Panel _pnlReplaceBanner;
    private readonly Label _lblReplaceBanner;
    private readonly Button _btnCancelReplace;
    private readonly FlowLayoutPanel _listPanel;
    private readonly Panel _hotbarPickerPanel;
    private readonly Button[] _hotbarSlotButtons = new Button[HOTBAR_SLOTS];
    private readonly Item?[] _hotbarSlotItems = new Item?[HOTBAR_SLOTS];
    private readonly int[] _hotbarSlotQuantities = new int[HOTBAR_SLOTS];
    private readonly Button _btnClose;

    private readonly List<Button> _dynamicCraftButtons = new();
    private readonly List<Button> _dynamicReplaceButtons = new();
    private readonly List<Button> _dynamicEquipButtons = new();
    private readonly List<(Item Item, int TotalQuantity)> _uniqueItemsBuffer = new();
    private readonly StringBuilder _textBuilder = new(320);

    private readonly Font _headerFont;
    private readonly Font _statusFont;
    private readonly Font _rowTitleFont;
    private readonly Font _rowBodyFont;
    private readonly Font _slotBadgeFont;
    private readonly SolidBrush _slotTextGoldBrush;
    private readonly SolidBrush _slotTextGreenBrush;
    private readonly SolidBrush _slotTextMutedBrush;

    private GameSession? _session;
    private IReadOnlyList<CraftingRecipe> _recipes = Array.Empty<CraftingRecipe>();
    private SpriteAtlasCache? _sprites;
    private float _workbenchRadius = 80f;
    private PanelTab _activeTab = PanelTab.Recipes;
    private string? _pendingReplaceItemId;

    /// <summary>Raised when the user clicks the Close button.</summary>
    public event Action? OnCloseRequested;

    /// <summary>Gets whether the panel is currently displaying the Backpack tab (for UI verification/telemetry).</summary>
    public bool IsBackpackTabActive => _activeTab == PanelTab.Backpack;

    /// <summary>Gets the item ID currently pending hotbar slot replacement, if any.</summary>
    public string? PendingReplaceItemId => _pendingReplaceItemId;

    /// <summary>Initializes a new instance of the <see cref="CraftingOverlayPanel"/> class.</summary>
    public CraftingOverlayPanel()
    {
        Size = new Size(700, 566);
        BackColor = UiTheme.BackgroundPrimary;
        BorderStyle = BorderStyle.FixedSingle;
        Visible = false;

        _headerFont = new Font(UiTheme.FontFamily, 11.5f, FontStyle.Bold);
        _statusFont = new Font(UiTheme.FontFamily, 9.2f, FontStyle.Bold);
        _rowTitleFont = new Font(UiTheme.FontFamily, 9.3f, FontStyle.Bold);
        _rowBodyFont = new Font(UiTheme.FontFamily, 8.5f);
        _slotBadgeFont = new Font(UiTheme.FontFamily, 7.8f, FontStyle.Bold);

        _slotTextGoldBrush = new SolidBrush(UiTheme.AccentGold);
        _slotTextGreenBrush = new SolidBrush(Color.FromArgb(124, 234, 152));
        _slotTextMutedBrush = new SolidBrush(UiTheme.TextMuted);

        var lblHeader = new Label
        {
            Text = "INVENTORY, BACKPACK & CRAFTING  (Press C or Tab to close)",
            UseMnemonic = false,
            Location = new Point(16, 12),
            Size = new Size(668, 26),
            Font = _headerFont,
            ForeColor = UiTheme.AccentGold
        };
        Controls.Add(lblHeader);

        _lblWorkbenchStatus = new Label
        {
            UseMnemonic = false,
            Location = new Point(16, 40),
            Size = new Size(668, 20),
            Font = _statusFont,
            ForeColor = Color.FromArgb(146, 226, 152)
        };
        Controls.Add(_lblWorkbenchStatus);

        _lblInventorySummary = new Label
        {
            UseMnemonic = false,
            Location = new Point(16, 63),
            Size = new Size(668, 36),
            Font = _rowBodyFont,
            ForeColor = UiTheme.TextSecondary
        };
        Controls.Add(_lblInventorySummary);

        _btnTabRecipes = new Button
        {
            Text = "Recipes",
            Location = new Point(16, 104),
            Size = new Size(128, 28),
            FlatStyle = FlatStyle.Flat,
            Font = _statusFont,
            Cursor = Cursors.Hand
        };
        _btnTabRecipes.FlatAppearance.BorderSize = 1;
        _btnTabRecipes.Click += HandleRecipesTabClick;
        Controls.Add(_btnTabRecipes);

        _btnTabBackpack = new Button
        {
            Text = "Backpack (All)",
            Location = new Point(150, 104),
            Size = new Size(150, 28),
            FlatStyle = FlatStyle.Flat,
            Font = _statusFont,
            Cursor = Cursors.Hand
        };
        _btnTabBackpack.FlatAppearance.BorderSize = 1;
        _btnTabBackpack.Click += HandleBackpackTabClick;
        Controls.Add(_btnTabBackpack);

        _lblFeedback = new Label
        {
            UseMnemonic = false,
            Location = new Point(310, 106),
            Size = new Size(374, 24),
            Font = _rowBodyFont,
            ForeColor = UiTheme.AccentGold,
            TextAlign = ContentAlignment.MiddleRight
        };
        Controls.Add(_lblFeedback);

        _pnlReplaceBanner = new Panel
        {
            Location = new Point(16, 138),
            Size = new Size(668, 32),
            BackColor = UiTheme.SurfaceHeader
        };
        Controls.Add(_pnlReplaceBanner);

        _lblReplaceBanner = new Label
        {
            UseMnemonic = false,
            Location = new Point(10, 5),
            Size = new Size(554, 22),
            Font = _rowBodyFont,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _pnlReplaceBanner.Controls.Add(_lblReplaceBanner);

        _btnCancelReplace = new Button
        {
            Text = "Cancel",
            Location = new Point(572, 4),
            Size = new Size(86, 24),
            FlatStyle = FlatStyle.Flat,
            Font = _rowBodyFont,
            BackColor = UiTheme.ButtonDanger,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Visible = false
        };
        _btnCancelReplace.FlatAppearance.BorderSize = 0;
        _btnCancelReplace.Click += HandleCancelReplaceClick;
        _pnlReplaceBanner.Controls.Add(_btnCancelReplace);

        _listPanel = new FlowLayoutPanel
        {
            Location = new Point(16, 176),
            Size = new Size(668, 248),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = UiTheme.SurfaceCard
        };
        _listPanel.HorizontalScroll.Maximum = 0;
        _listPanel.HorizontalScroll.Visible = false;
        Controls.Add(_listPanel);

        _hotbarPickerPanel = new Panel
        {
            Location = new Point(16, 432),
            Size = new Size(668, 82),
            BackColor = UiTheme.SurfaceHeader
        };
        Controls.Add(_hotbarPickerPanel);

        const int slotW = 70;
        const int slotGap = 4;
        for (int i = 0; i < HOTBAR_SLOTS; i++)
        {
            int x = 3 + (i * (slotW + slotGap));
            var btn = new Button
            {
                Location = new Point(x, 4),
                Size = new Size(slotW, 74),
                FlatStyle = FlatStyle.Flat,
                Tag = i,
                Text = string.Empty,
                Cursor = Cursors.Hand,
                BackColor = i < Player.WEAPON_ONLY_SLOT_COUNT
                    ? Color.FromArgb(44, 28, 38)
                    : UiTheme.SurfaceInput,
                ForeColor = UiTheme.TextPrimary
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += HandleHotbarSlotClick;
            btn.Paint += HandleHotbarSlotPaint;
            _hotbarSlotButtons[i] = btn;
            _hotbarPickerPanel.Controls.Add(btn);
        }

        var lblTip = new Label
        {
            Text = "Hotbar Picker: [1W–3W] Weapons Only  |  [4–9] Any Food, Tool, Weapon or Structure",
            UseMnemonic = false,
            Location = new Point(16, 526),
            Size = new Size(548, 24),
            Font = _rowBodyFont,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(lblTip);

        _btnClose = new Button
        {
            Text = "Close (C)",
            Location = new Point(578, 521),
            Size = new Size(106, 34),
            FlatStyle = FlatStyle.Flat,
            Font = _statusFont,
            BackColor = UiTheme.ButtonNeutral,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _btnClose.FlatAppearance.BorderSize = 0;
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
        _pendingReplaceItemId = null;
        _lblFeedback.Text = string.Empty;
        RefreshView();
    }

    /// <summary>Switches to the Backpack tab programmatically (useful for tests/shortcuts) and refreshes.</summary>
    public void SwitchToBackpackTab()
    {
        _activeTab = PanelTab.Backpack;
        RefreshView();
    }

    /// <summary>Switches to the Recipes tab and refreshes.</summary>
    public void SwitchToRecipesTab()
    {
        _activeTab = PanelTab.Recipes;
        RefreshView();
    }

    /// <summary>Enters Hotbar Replace selection mode for <paramref name="itemId"/> and refreshes slot availability.</summary>
    public void BeginReplaceForItem(string itemId)
    {
        _pendingReplaceItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        _lblFeedback.Text = string.Empty;
        RefreshView();
    }

    /// <summary>Exposes the 9 hotbar slot picker buttons cleanly for UI automation or verification.</summary>
    public Button GetHotbarPickerButton(int slotIndex) => _hotbarSlotButtons[slotIndex];

    /// <summary>Rebuilds recipe/backpack rows and updates hotbar picker availability from Core rules.</summary>
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

        UpdateTabAppearance();
        UpdateReplaceBanner(_session);
        RefreshHotbarPicker(_session);

        _listPanel.SuspendLayout();
        ClearListRows();
        if (_activeTab == PanelTab.Recipes)
        {
            PopulateRecipeRows(_session, nearWorkbench);
        }
        else
        {
            PopulateBackpackRows(_session);
        }

        _listPanel.ResumeLayout();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _btnClose.Click -= HandleCloseClick;
            _btnTabRecipes.Click -= HandleRecipesTabClick;
            _btnTabBackpack.Click -= HandleBackpackTabClick;
            _btnCancelReplace.Click -= HandleCancelReplaceClick;
            for (int i = 0; i < HOTBAR_SLOTS; i++)
            {
                _hotbarSlotButtons[i].Click -= HandleHotbarSlotClick;
                _hotbarSlotButtons[i].Paint -= HandleHotbarSlotPaint;
            }

            ClearListRows();
            _headerFont.Dispose();
            _statusFont.Dispose();
            _rowTitleFont.Dispose();
            _rowBodyFont.Dispose();
            _slotBadgeFont.Dispose();
            _slotTextGoldBrush.Dispose();
            _slotTextGreenBrush.Dispose();
            _slotTextMutedBrush.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateTabAppearance()
    {
        bool recipesActive = _activeTab == PanelTab.Recipes;
        _btnTabRecipes.BackColor = recipesActive ? UiTheme.AccentBlue : UiTheme.ButtonNeutral;
        _btnTabRecipes.ForeColor = Color.White;
        _btnTabRecipes.FlatAppearance.BorderColor = recipesActive ? UiTheme.AccentGold : UiTheme.BorderSubtle;

        _btnTabBackpack.BackColor = !recipesActive ? UiTheme.AccentBlue : UiTheme.ButtonNeutral;
        _btnTabBackpack.ForeColor = Color.White;
        _btnTabBackpack.FlatAppearance.BorderColor = !recipesActive ? UiTheme.AccentGold : UiTheme.BorderSubtle;
    }

    private void UpdateReplaceBanner(GameSession session)
    {
        if (_pendingReplaceItemId is not null)
        {
            ItemStack? owned = session.Inventory.FindFirstStack(_pendingReplaceItemId);
            if (owned is null)
            {
                _pendingReplaceItemId = null;
            }
            else
            {
                bool isWeapon = owned.Item.Category == ItemCategory.Weapon;
                _pnlReplaceBanner.BackColor = UiTheme.RowHighlight;
                _lblReplaceBanner.ForeColor = UiTheme.AccentGold;
                _lblReplaceBanner.Text = isWeapon
                    ? $"REPLACE MODE: Click any hotbar slot [1W–9] to equip '{owned.Item.Name}'."
                    : $"REPLACE MODE: Click slot [4–9] to bind '{owned.Item.Name}' (1W–3W: Weapons only).";
                _btnCancelReplace.Visible = true;
                return;
            }
        }

        _pnlReplaceBanner.BackColor = UiTheme.SurfaceHeader;
        _lblReplaceBanner.ForeColor = UiTheme.TextMuted;
        _lblReplaceBanner.Text = "Click [Replace] on any owned item or recipe, then choose a hotbar slot [1W–9] below.";
        _btnCancelReplace.Visible = false;
    }

    private void RefreshHotbarPicker(GameSession session)
    {
        Item? pendingItem = _pendingReplaceItemId is not null
            ? session.Inventory.FindFirstStack(_pendingReplaceItemId)?.Item
            : null;
        int selectedHotbar = session.Player.SelectedHotbarIndex;

        for (int i = 0; i < HOTBAR_SLOTS; i++)
        {
            session.Player.TryGetHotbarEntry(i, out Item? slotItem, out int slotQty);
            _hotbarSlotItems[i] = slotItem;
            _hotbarSlotQuantities[i] = slotQty;

            Button btn = _hotbarSlotButtons[i];
            bool isWeaponSlot = i < Player.WEAPON_ONLY_SLOT_COUNT;
            if (pendingItem is not null)
            {
                bool allowed = session.Player.CanAssignToHotbarSlot(pendingItem, i, out _);
                btn.Enabled = allowed;
                btn.BackColor = allowed
                    ? Color.FromArgb(34, 78, 56)
                    : Color.FromArgb(32, 26, 30);
                btn.FlatAppearance.BorderColor = allowed ? UiTheme.AccentGold : UiTheme.BorderSubtle;
                btn.FlatAppearance.BorderSize = allowed ? 2 : 1;
            }
            else
            {
                btn.Enabled = true;
                bool isSelected = i == selectedHotbar;
                btn.BackColor = isSelected
                    ? Color.FromArgb(52, 74, 106)
                    : (isWeaponSlot ? Color.FromArgb(44, 28, 38) : UiTheme.SurfaceInput);
                btn.FlatAppearance.BorderColor = isSelected ? UiTheme.AccentGold : UiTheme.BorderSubtle;
                btn.FlatAppearance.BorderSize = isSelected ? 2 : 1;
            }

            btn.Invalidate();
        }
    }

    private void PopulateRecipeRows(GameSession session, bool nearWorkbench)
    {
        for (int i = 0; i < _recipes.Count; i++)
        {
            CraftingRecipe recipe = _recipes[i];
            bool canCraft = session.CraftingSystem.CanCraft(session.Inventory, recipe, nearWorkbench);
            bool ownsOutput = session.Inventory.GetItemCount(recipe.OutputItemId) > 0;
            _listPanel.Controls.Add(CreateRecipeRow(recipe, canCraft, ownsOutput));
        }
    }

    private void PopulateBackpackRows(GameSession session)
    {
        session.Inventory.GetUniqueItems(_uniqueItemsBuffer);
        if (_uniqueItemsBuffer.Count == 0)
        {
            _listPanel.Controls.Add(new Label
            {
                Text = "Your unlimited backpack is currently empty. Gather wood/stone or hunt animals!",
                UseMnemonic = false,
                Size = new Size(636, 48),
                Font = _rowBodyFont,
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter
            });
            return;
        }

        for (int i = 0; i < _uniqueItemsBuffer.Count; i++)
        {
            (Item item, int totalQuantity) = _uniqueItemsBuffer[i];
            _listPanel.Controls.Add(CreateBackpackRow(session, item, totalQuantity));
        }
    }

    private Panel CreateRecipeRow(CraftingRecipe recipe, bool canCraft, bool ownsOutput)
    {
        bool isPendingTarget = string.Equals(_pendingReplaceItemId, recipe.OutputItemId, StringComparison.OrdinalIgnoreCase);
        var row = new Panel
        {
            Size = new Size(638, 60),
            Margin = new Padding(4, 3, 4, 3),
            BackColor = isPendingTarget
                ? UiTheme.RowHighlight
                : (canCraft ? Color.FromArgb(32, 72, 48) : Color.FromArgb(62, 34, 40))
        };

        string ingText = BuildIngredientsText(recipe.Ingredients);
        string wbTag = recipe.RequiresWorkbench ? " [WB]" : " [Hand]";

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

        int textLeft = icon is not null ? 54 : 12;
        int ownedCount = _session?.Inventory.GetItemCount(recipe.OutputItemId) ?? 0;
        string ownedSuffix = ownedCount > 0 ? $"  (Bag: {ownedCount})" : string.Empty;

        row.Controls.Add(new Label
        {
            Text = $"{FormatDisplayName(recipe.OutputItemId)} x{recipe.OutputQuantity}{wbTag}{ownedSuffix}",
            UseMnemonic = false,
            Location = new Point(textLeft, 6),
            Size = new Size(396, 22),
            Font = _rowTitleFont,
            ForeColor = canCraft ? Color.FromArgb(174, 248, 188) : Color.FromArgb(246, 174, 174)
        });

        row.Controls.Add(new Label
        {
            Text = $"Materials: {ingText}",
            UseMnemonic = false,
            Location = new Point(textLeft, 30),
            Size = new Size(396, 22),
            Font = _rowBodyFont,
            ForeColor = UiTheme.TextPrimary
        });

        var btnCraft = new Button
        {
            Text = "Craft",
            Location = new Point(460, 14),
            Size = new Size(80, 32),
            Enabled = canCraft,
            Tag = recipe,
            FlatStyle = FlatStyle.Flat,
            Font = _rowBodyFont,
            BackColor = canCraft ? UiTheme.AccentGreen : Color.FromArgb(84, 58, 62),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnCraft.FlatAppearance.BorderSize = 0;
        btnCraft.Click += HandleCraftButtonClick;
        _dynamicCraftButtons.Add(btnCraft);
        row.Controls.Add(btnCraft);

        var btnReplace = new Button
        {
            Text = isPendingTarget ? "Picking..." : "Replace",
            Location = new Point(546, 14),
            Size = new Size(82, 32),
            Enabled = ownsOutput,
            Tag = recipe.OutputItemId,
            FlatStyle = FlatStyle.Flat,
            Font = _rowBodyFont,
            BackColor = !ownsOutput
                ? Color.FromArgb(52, 58, 70)
                : (isPendingTarget ? UiTheme.AccentGoldDark : UiTheme.AccentBlue),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnReplace.FlatAppearance.BorderSize = 0;
        btnReplace.Click += HandleReplaceButtonClick;
        _dynamicReplaceButtons.Add(btnReplace);
        row.Controls.Add(btnReplace);

        return row;
    }

    private Panel CreateBackpackRow(GameSession session, Item item, int totalQuantity)
    {
        bool isPendingTarget = string.Equals(_pendingReplaceItemId, item.Id, StringComparison.OrdinalIgnoreCase);
        bool isArmor = item is ArmorItem;
        int boundSlot = FindBoundHotbarSlot(session.Player, item.Id);
        string boundLabel = boundSlot >= 0 ? $"  • Hotbar [{HOTBAR_SLOT_BADGES[boundSlot]}]" : string.Empty;

        var row = new Panel
        {
            Size = new Size(638, 56),
            Margin = new Padding(4, 3, 4, 3),
            BackColor = isPendingTarget ? UiTheme.RowHighlight : UiTheme.RowEven
        };

        Bitmap? icon = _sprites?.Get(item.Id);
        if (icon is not null)
        {
            row.Controls.Add(new PictureBox
            {
                Image = icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(10, 10),
                Size = new Size(36, 36),
                BackColor = Color.Transparent
            });
        }

        int textLeft = icon is not null ? 54 : 12;
        row.Controls.Add(new Label
        {
            Text = $"{item.Name} x{totalQuantity}  [{item.Category}]{boundLabel}",
            UseMnemonic = false,
            Location = new Point(textLeft, 6),
            Size = new Size(396, 22),
            Font = _rowTitleFont,
            ForeColor = item.Category == ItemCategory.Weapon ? UiTheme.AccentGold : UiTheme.TextPrimary
        });

        string slotRuleText = item.Category == ItemCategory.Weapon
            ? "Allowed in Hotbar [1W–3W] or [4–9]"
            : (isArmor ? "Equip to Armor Slot or bind [4–9]" : "Allowed in Hotbar [4–9]");
        row.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(item.Description) ? slotRuleText : $"{slotRuleText} • {item.Description}",
            UseMnemonic = false,
            Location = new Point(textLeft, 29),
            Size = new Size(396, 20),
            Font = _rowBodyFont,
            ForeColor = UiTheme.TextSecondary
        });

        if (isArmor)
        {
            var btnEquip = new Button
            {
                Text = "Equip",
                Location = new Point(460, 12),
                Size = new Size(80, 32),
                Tag = item.Id,
                FlatStyle = FlatStyle.Flat,
                Font = _rowBodyFont,
                BackColor = UiTheme.AccentGreen,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnEquip.FlatAppearance.BorderSize = 0;
            btnEquip.Click += HandleEquipArmorButtonClick;
            _dynamicEquipButtons.Add(btnEquip);
            row.Controls.Add(btnEquip);
        }

        var btnReplace = new Button
        {
            Text = isPendingTarget ? "Picking..." : "Replace",
            Location = new Point(546, 12),
            Size = new Size(82, 32),
            Tag = item.Id,
            FlatStyle = FlatStyle.Flat,
            Font = _rowBodyFont,
            BackColor = isPendingTarget ? UiTheme.AccentGoldDark : UiTheme.AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnReplace.FlatAppearance.BorderSize = 0;
        btnReplace.Click += HandleReplaceButtonClick;
        _dynamicReplaceButtons.Add(btnReplace);
        row.Controls.Add(btnReplace);

        return row;
    }

    private void ClearListRows()
    {
        for (int i = 0; i < _dynamicCraftButtons.Count; i++)
        {
            _dynamicCraftButtons[i].Click -= HandleCraftButtonClick;
        }
        _dynamicCraftButtons.Clear();

        for (int i = 0; i < _dynamicReplaceButtons.Count; i++)
        {
            _dynamicReplaceButtons[i].Click -= HandleReplaceButtonClick;
        }
        _dynamicReplaceButtons.Clear();

        for (int i = 0; i < _dynamicEquipButtons.Count; i++)
        {
            _dynamicEquipButtons[i].Click -= HandleEquipArmorButtonClick;
        }
        _dynamicEquipButtons.Clear();

        if (_listPanel.Controls.Count == 0)
        {
            return;
        }

        var removed = new Control[_listPanel.Controls.Count];
        _listPanel.Controls.CopyTo(removed, 0);
        _listPanel.Controls.Clear();

        for (int i = 0; i < removed.Length; i++)
        {
            Control row = removed[i];
            DetachSharedImages(row);
            row.Dispose();
        }
    }

    private static void DetachSharedImages(Control container)
    {
        for (int i = 0; i < container.Controls.Count; i++)
        {
            if (container.Controls[i] is PictureBox pb)
            {
                // Shared Bitmap belongs to SpriteAtlasCache; do not let PictureBox.Dispose() dispose it.
                pb.Image = null;
            }
        }
    }

    private static int FindBoundHotbarSlot(Player player, string itemId)
    {
        for (int i = 0; i < HOTBAR_SLOTS; i++)
        {
            if (player.TryGetHotbarEntry(i, out Item? slotItem, out _) &&
                slotItem is not null &&
                string.Equals(slotItem.Id, itemId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleRecipesTabClick(object? sender, EventArgs e)
    {
        if (_activeTab != PanelTab.Recipes)
        {
            _activeTab = PanelTab.Recipes;
            RefreshView();
        }
    }

    private void HandleBackpackTabClick(object? sender, EventArgs e)
    {
        if (_activeTab != PanelTab.Backpack)
        {
            _activeTab = PanelTab.Backpack;
            RefreshView();
        }
    }

    private void HandleCancelReplaceClick(object? sender, EventArgs e)
    {
        _pendingReplaceItemId = null;
        _lblFeedback.Text = "Hotbar replace cancelled.";
        _lblFeedback.ForeColor = UiTheme.TextMuted;
        RefreshView();
    }

    private void HandleCraftButtonClick(object? sender, EventArgs e)
    {
        if (_session is null || sender is not Button { Tag: CraftingRecipe recipe })
        {
            return;
        }

        if (_session.TryCraft(recipe, _workbenchRadius))
        {
            _lblFeedback.Text = $"Crafted {FormatDisplayName(recipe.OutputItemId)} x{recipe.OutputQuantity}.";
            _lblFeedback.ForeColor = Color.FromArgb(124, 234, 152);
            RefreshView();
        }
    }

    private void HandleReplaceButtonClick(object? sender, EventArgs e)
    {
        if (sender is not Button { Tag: string itemId } || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        BeginReplaceForItem(itemId);
    }

    private void HandleEquipArmorButtonClick(object? sender, EventArgs e)
    {
        if (_session is null || sender is not Button { Tag: string itemId })
        {
            return;
        }

        if (_session.TryEquipArmorFromInventory(itemId))
        {
            _lblFeedback.Text = $"Equipped {FormatDisplayName(itemId)} (Total DEF: {_session.Player.TotalDefense}).";
            _lblFeedback.ForeColor = Color.FromArgb(124, 234, 152);
            RefreshView();
        }
    }

    private void HandleHotbarSlotClick(object? sender, EventArgs e)
    {
        if (_session is null || sender is not Button { Tag: int slotIdx })
        {
            return;
        }

        if (_pendingReplaceItemId is null)
        {
            _session.Player.SelectHotbarSlot(slotIdx);
            _lblFeedback.Text = $"Selected Hotbar [{HOTBAR_SLOT_BADGES[slotIdx]}].";
            _lblFeedback.ForeColor = UiTheme.TextSecondary;
            RefreshView();
            return;
        }

        string targetItemId = _pendingReplaceItemId;
        if (_session.Player.TryReplaceHotbarSlot(targetItemId, slotIdx, out string? reason))
        {
            ItemStack? replaced = _session.Inventory.FindFirstStack(targetItemId);
            string displayName = replaced?.Item.Name ?? FormatDisplayName(targetItemId);
            _pendingReplaceItemId = null;
            _lblFeedback.Text = $"Bound {displayName} to Hotbar [{HOTBAR_SLOT_BADGES[slotIdx]}].";
            _lblFeedback.ForeColor = Color.FromArgb(124, 234, 152);
            RefreshView();
        }
        else
        {
            _lblFeedback.Text = reason ?? "Cannot bind item to that slot.";
            _lblFeedback.ForeColor = Color.FromArgb(244, 132, 132);
        }
    }

    private void HandleHotbarSlotPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not Button { Tag: int slotIdx } btn || slotIdx < 0 || slotIdx >= HOTBAR_SLOTS)
        {
            return;
        }

        Graphics g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawString(HOTBAR_SLOT_BADGES[slotIdx], _slotBadgeFont, _slotTextGoldBrush, 4f, 3f);

        Item? item = _hotbarSlotItems[slotIdx];
        int qty = _hotbarSlotQuantities[slotIdx];
        if (item is null || qty <= 0)
        {
            g.DrawString("Empty", _slotBadgeFont, _slotTextMutedBrush, 14f, 31f);
            return;
        }

        Bitmap? icon = _sprites?.Get(item.Id);
        if (icon is not null)
        {
            g.DrawImage(icon, (btn.Width - 30) / 2, 18, 30, 30);
        }

        string qtyText = qty >= 100 ? "99+" : $"x{qty}";
        g.DrawString(qtyText, _slotBadgeFont, _slotTextGreenBrush, btn.Width - 30f, btn.Height - 17f);
    }

    private void HandleCloseClick(object? sender, EventArgs e)
    {
        _pendingReplaceItemId = null;
        OnCloseRequested?.Invoke();
    }

    private string BuildIngredientsText(IReadOnlyList<RecipeIngredient> ingredients)
    {
        _textBuilder.Clear();
        for (int i = 0; i < ingredients.Count; i++)
        {
            if (i > 0)
            {
                _textBuilder.Append("  |  ");
            }

            RecipeIngredient ing = ingredients[i];
            int owned = _session?.Inventory.GetItemCount(ing.ItemId) ?? 0;
            _textBuilder.Append(FormatDisplayName(ing.ItemId));
            _textBuilder.Append(' ');
            _textBuilder.Append(owned);
            _textBuilder.Append('/');
            _textBuilder.Append(ing.Quantity);
        }

        return _textBuilder.ToString();
    }

    private string BuildInventorySummaryText(GameSession session)
    {
        session.Inventory.GetUniqueItems(_uniqueItemsBuffer);
        if (_uniqueItemsBuffer.Count == 0)
        {
            return session.Inventory.IsUnlimited ? "Bag (Unlimited): empty" : "Bag: empty";
        }

        int totalItems = 0;
        for (int i = 0; i < _uniqueItemsBuffer.Count; i++)
        {
            totalItems += _uniqueItemsBuffer[i].TotalQuantity;
        }

        _textBuilder.Clear();
        if (session.Inventory.IsUnlimited)
        {
            _textBuilder.Append("Bag (Unlimited — ");
            _textBuilder.Append(_uniqueItemsBuffer.Count);
            _textBuilder.Append(" types, ");
            _textBuilder.Append(totalItems);
            _textBuilder.Append(" items): ");
        }
        else
        {
            _textBuilder.Append("Bag (");
            _textBuilder.Append(session.Inventory.UsedSlots);
            _textBuilder.Append('/');
            _textBuilder.Append(session.Inventory.Capacity);
            _textBuilder.Append("): ");
        }

        int previewLimit = Math.Min(_uniqueItemsBuffer.Count, 8);
        for (int i = 0; i < previewLimit; i++)
        {
            if (i > 0)
            {
                _textBuilder.Append(" • ");
            }

            (Item item, int qty) = _uniqueItemsBuffer[i];
            _textBuilder.Append(item.Name);
            _textBuilder.Append(" x");
            _textBuilder.Append(qty);
        }

        if (_uniqueItemsBuffer.Count > previewLimit)
        {
            _textBuilder.Append(" • +");
            _textBuilder.Append(_uniqueItemsBuffer.Count - previewLimit);
            _textBuilder.Append(" more (see Backpack)");
        }

        return _textBuilder.ToString();
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
