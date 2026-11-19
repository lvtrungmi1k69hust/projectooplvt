using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a placeable structure item such as a workbench, wooden wall, stone wall, or torch.
/// </summary>
public sealed class StructureItem : Item
{
    private readonly StructureType _structureType;

    /// <summary>Gets the structure archetype created when placed in the world.</summary>
    public StructureType StructureType => _structureType;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructureItem"/> class.
    /// </summary>
    public StructureItem(
        string id,
        string name,
        StructureType structureType,
        int maxStackSize = 50,
        string description = "")
        : base(id, name, ItemCategory.Structure, maxStackSize, description)
    {
        _structureType = structureType;
    }

    /// <inheritdoc />
    public override bool TryGetPlaceableStructure(out StructureType structureType)
    {
        structureType = _structureType;
        return true;
    }
}
