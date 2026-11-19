using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a stackable crafting material such as wood, stone, copper ore, or slime gel.
/// </summary>
public sealed class MaterialItem : Item
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialItem"/> class.
    /// </summary>
    public MaterialItem(string id, string name, int maxStackSize = 99, string description = "")
        : base(id, name, ItemCategory.Material, maxStackSize, description)
    {
    }
}
