using System;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a bounded stack of identical <see cref="Item"/> units inside an <see cref="Inventory"/>.
/// Enforces the invariant 0 &lt;= Quantity &lt;= Item.MaxStackSize.
/// </summary>
public sealed class ItemStack
{
    private readonly Item _item;
    private int _quantity;

    /// <summary>Gets the item definition held by this stack.</summary>
    public Item Item => _item;

    /// <summary>Gets the current unit count in this stack.</summary>
    public int Quantity => _quantity;

    /// <summary>Gets the remaining space before this stack reaches <see cref="Item.MaxStackSize"/>.</summary>
    public int RemainingCapacity => _item.MaxStackSize - _quantity;

    /// <summary>Gets a value indicating whether this stack has zero remaining units.</summary>
    public bool IsEmpty => _quantity == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemStack"/> class.
    /// </summary>
    public ItemStack(Item item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantity <= 0 || quantity > item.MaxStackSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"Stack quantity must be within [1, {item.MaxStackSize}].");
        }

        _item = item;
        _quantity = quantity;
    }

    /// <summary>
    /// Adds up to <paramref name="amount"/> units into this stack without exceeding <see cref="Item.MaxStackSize"/>.
    /// </summary>
    /// <returns>The number of overflow units that could not fit in this stack.</returns>
    public int Add(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount to add must be positive.");
        }

        int space = RemainingCapacity;
        int added = global::System.Math.Min(space, amount);
        _quantity += added;
        return amount - added;
    }

    /// <summary>
    /// Removes exactly <paramref name="amount"/> units from this stack.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is not within [1, Quantity].</exception>
    public void Remove(int amount)
    {
        if (amount <= 0 || amount > _quantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"Removal amount must be within [1, {_quantity}].");
        }

        _quantity -= amount;
    }
}
