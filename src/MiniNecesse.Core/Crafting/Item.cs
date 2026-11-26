using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Abstract base class for all inventory items.
/// Uses polymorphic virtual hooks to handle hotbar selection, usage, and placement without type casting.
/// </summary>
public abstract class Item
{
    private readonly string _id;
    private readonly string _name;
    private readonly ItemCategory _category;
    private readonly int _maxStackSize;
    private readonly string _description;

    /// <summary>Gets the unique catalog identifier of this item (e.g., "wood", "copper_sword").</summary>
    public string Id => _id;

    /// <summary>Gets the player-facing display name of this item.</summary>
    public string Name => _name;

    /// <summary>Gets the functional category of this item.</summary>
    public ItemCategory Category => _category;

    /// <summary>Gets the maximum number of units that fit in one inventory stack.</summary>
    public int MaxStackSize => _maxStackSize;

    /// <summary>Gets a short description of this item's purpose.</summary>
    public string Description => _description;

    /// <summary>
    /// Initializes a new instance of the <see cref="Item"/> class.
    /// </summary>
    protected Item(string id, string name, ItemCategory category, int maxStackSize, string description = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Item id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name cannot be empty.", nameof(name));
        }

        if (maxStackSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStackSize), "Max stack size must be positive.");
        }

        _id = id;
        _name = name;
        _category = category;
        _maxStackSize = maxStackSize;
        _description = description ?? string.Empty;
    }

    /// <summary>
    /// Polymorphic hook invoked when the player selects this item on the hotbar.
    /// Defaults to unequipping weapons and tools.
    /// </summary>
    public virtual void ApplyHotbarSelection(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.EquipWeapon(null);
        player.UnequipTool();
    }

    /// <summary>
    /// Polymorphic hook invoked when the player uses this item (e.g., drinking a potion).
    /// Returns <see langword="true"/> if one unit of the item should be consumed.
    /// </summary>
    public virtual bool TryUse(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return false;
    }

    /// <summary>
    /// Polymorphic hook indicating whether this item places a world structure on right-click.
    /// </summary>
    public virtual bool TryGetPlaceableStructure(out StructureType structureType)
    {
        structureType = default;
        return false;
    }

    /// <summary>
    /// Polymorphic hook indicating whether this item plants a crop seed onto tilled soil.
    /// </summary>
    public virtual bool TryGetPlantableCropId(out string cropId)
    {
        cropId = string.Empty;
        return false;
    }
}
