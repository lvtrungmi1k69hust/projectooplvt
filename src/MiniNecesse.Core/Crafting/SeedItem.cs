using System;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a plantable seed packet linked polymorphically to a crop definition identifier.
/// </summary>
public sealed class SeedItem : Item
{
    private readonly string _cropId;

    /// <summary>Gets the identifier of the crop grown when this seed is planted.</summary>
    public string CropId => _cropId;

    /// <summary>Initializes a new instance of the <see cref="SeedItem"/> class.</summary>
    public SeedItem(
        string id,
        string name,
        string cropId,
        int maxStackSize = 50,
        string description = "")
        : base(id, name, ItemCategory.Seed, maxStackSize, description)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new ArgumentException("CropId cannot be empty.", nameof(cropId));
        }

        _cropId = cropId;
    }

    /// <inheritdoc />
    public override bool TryGetPlantableCropId(out string cropId)
    {
        cropId = _cropId;
        return true;
    }
}
