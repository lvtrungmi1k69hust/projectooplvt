using System;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Contract for loose world items that can be picked up into a receiver container.
/// </summary>
public interface ICollectible
{
    /// <summary>Gets the identifier of the item represented by this pickup.</summary>
    string ItemId { get; }

    /// <summary>Gets the positive quantity of items in this pickup.</summary>
    int Quantity { get; }

    /// <summary>Gets a value indicating whether this pickup has already been collected.</summary>
    bool IsCollected { get; }

    /// <summary>Raised immediately after this item is successfully collected.</summary>
    event Action<ICollectible>? OnCollected;

    /// <summary>
    /// Attempts to transfer this collectible using the supplied storage callback.
    /// Marks the collectible as collected only if <paramref name="tryStore"/> returns <see langword="true"/>.
    /// </summary>
    bool TryCollect(Func<string, int, bool> tryStore);
}
