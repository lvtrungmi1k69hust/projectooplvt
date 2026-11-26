using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Manages player-carried item stacks with bounded slot capacity.
/// Demonstrates UML Aggregation by referencing external <see cref="Item"/> / <see cref="ItemStack"/> instances
/// and exposes <see cref="IReadOnlyList{T}"/> to protect internal invariants.
/// </summary>
public sealed class Inventory
{
    private readonly int _capacity;
    private readonly List<ItemStack> _stacks;
    private readonly IReadOnlyList<ItemStack> _stacksView;

    /// <summary>Gets the maximum number of distinct item stacks allowed in this inventory.</summary>
    public int Capacity => _capacity;

    /// <summary>Gets the number of currently occupied inventory slots.</summary>
    public int UsedSlots => _stacks.Count;

    /// <summary>Gets the number of empty inventory slots remaining.</summary>
    public int FreeSlots => _capacity - _stacks.Count;

    /// <summary>Gets a cached read-only view of all active stacks to protect internal collection invariants.</summary>
    public IReadOnlyList<ItemStack> Stacks => _stacksView;

    /// <summary>Raised whenever items are added, removed, or cleared.</summary>
    public event Action? OnInventoryChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Inventory"/> class.
    /// </summary>
    public Inventory(int capacity = 20)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Inventory capacity must be positive.");
        }

        _capacity = capacity;
        _stacks = new List<ItemStack>(capacity);
        _stacksView = _stacks.AsReadOnly();
    }

    /// <summary>Returns the total quantity of <paramref name="itemId"/> across all stacks.</summary>
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < _stacks.Count; i++)
        {
            if (string.Equals(_stacks[i].Item.Id, itemId, StringComparison.Ordinal))
            {
                total += _stacks[i].Quantity;
            }
        }

        return total;
    }

    /// <summary>Checks whether the inventory holds at least <paramref name="requiredQuantity"/> of <paramref name="itemId"/>.</summary>
    public bool HasItem(string itemId, int requiredQuantity)
    {
        return requiredQuantity > 0 && GetItemCount(itemId) >= requiredQuantity;
    }

    /// <summary>Returns the stack at the given 0-based index, or <see langword="null"/> if empty/out of range.</summary>
    public ItemStack? GetStackAt(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _stacks.Count ? _stacks[slotIndex] : null;
    }

    /// <summary>Determines whether <paramref name="quantity"/> units of <paramref name="item"/> can fit atomically.</summary>
    public bool CanAddItem(Item item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (quantity <= 0)
        {
            return false;
        }

        int availableCapacity = CalculateAvailableCapacity(item);
        return availableCapacity >= quantity;
    }

    /// <summary>
    /// Atomically adds <paramref name="quantity"/> units of <paramref name="item"/>,
    /// stacking into existing slots before allocating new ones.
    /// </summary>
    public bool TryAddItem(Item item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (quantity <= 0 || !CanAddItem(item, quantity))
        {
            return false;
        }

        int remaining = FillExistingStacks(item.Id, quantity);
        CreateNewStacks(item, remaining);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Atomically adds an existing <see cref="ItemStack"/> reference into the inventory (Aggregation).</summary>
    public bool TryAddStack(ItemStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        return !stack.IsEmpty && TryAddItem(stack.Item, stack.Quantity);
    }

    /// <summary>
    /// Atomically removes <paramref name="quantity"/> units of <paramref name="itemId"/>.
    /// Returns <see langword="false"/> without mutating state if insufficient quantity is available.
    /// </summary>
    public bool TryRemoveItem(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0 || !HasItem(itemId, quantity))
        {
            return false;
        }

        int remaining = quantity;
        for (int i = _stacks.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (!string.Equals(_stacks[i].Item.Id, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            int take = global::System.Math.Min(_stacks[i].Quantity, remaining);
            _stacks[i].Remove(take);
            remaining -= take;

            if (_stacks[i].IsEmpty)
            {
                _stacks.RemoveAt(i);
            }
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Removes all stacks from the inventory.</summary>
    public void Clear()
    {
        if (_stacks.Count == 0)
        {
            return;
        }

        _stacks.Clear();
        OnInventoryChanged?.Invoke();
    }

    private int CalculateAvailableCapacity(Item item)
    {
        int capacity = FreeSlots * item.MaxStackSize;
        for (int i = 0; i < _stacks.Count; i++)
        {
            if (string.Equals(_stacks[i].Item.Id, item.Id, StringComparison.Ordinal))
            {
                capacity += _stacks[i].RemainingCapacity;
            }
        }

        return capacity;
    }

    private int FillExistingStacks(string itemId, int quantity)
    {
        int remaining = quantity;
        for (int i = 0; i < _stacks.Count && remaining > 0; i++)
        {
            if (string.Equals(_stacks[i].Item.Id, itemId, StringComparison.Ordinal) && _stacks[i].RemainingCapacity > 0)
            {
                remaining = _stacks[i].Add(remaining);
            }
        }

        return remaining;
    }

    private void CreateNewStacks(Item item, int remaining)
    {
        while (remaining > 0)
        {
            int batch = global::System.Math.Min(item.MaxStackSize, remaining);
            _stacks.Add(new ItemStack(item, batch));
            remaining -= batch;
        }
    }
}
