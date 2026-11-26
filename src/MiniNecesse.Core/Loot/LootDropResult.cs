namespace MiniNecesse.Core.Loot;

/// <summary>
/// Value-type result representing one rolled loot drop stack (zero heap allocation).
/// </summary>
public readonly record struct LootDropResult(string ItemId, int Quantity);
