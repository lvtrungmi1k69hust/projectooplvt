using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Farming;

/// <summary>
/// Registry mapping crop definitions by both <see cref="CropDefinition.CropId"/>
/// and <see cref="CropDefinition.SeedItemId"/> (OCP, zero hardcoded switch blocks).
/// </summary>
public sealed class CropRegistry
{
    private readonly Dictionary<string, CropDefinition> _byCropId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CropDefinition> _bySeedItemId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets total registered crop definitions.</summary>
    public int Count => _byCropId.Count;

    /// <summary>Registers or replaces a crop definition.</summary>
    public void Register(CropDefinition crop)
    {
        ArgumentNullException.ThrowIfNull(crop);
        _byCropId[crop.CropId] = crop;
        _bySeedItemId[crop.SeedItemId] = crop;
    }

    /// <summary>Registers a collection of crop definitions.</summary>
    public void RegisterRange(IEnumerable<CropDefinition> crops)
    {
        ArgumentNullException.ThrowIfNull(crops);
        foreach (CropDefinition crop in crops)
        {
            Register(crop);
        }
    }

    /// <summary>Looks up a crop definition by crop identifier.</summary>
    public bool TryGetById(string cropId, out CropDefinition? crop)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            crop = null;
            return false;
        }

        return _byCropId.TryGetValue(cropId, out crop);
    }

    /// <summary>Looks up a crop definition by plantable seed item identifier.</summary>
    public bool TryGetBySeedItemId(string seedItemId, out CropDefinition? crop)
    {
        if (string.IsNullOrWhiteSpace(seedItemId))
        {
            crop = null;
            return false;
        }

        return _bySeedItemId.TryGetValue(seedItemId, out crop);
    }

    /// <summary>Creates a registry pre-populated with default wheat, carrot, and pumpkin crops.</summary>
    public static CropRegistry CreateDefault()
    {
        var registry = new CropRegistry();
        registry.Register(new CropDefinition("wheat_crop", "Golden Wheat", "wheat_seed", "Wheat Seeds", "wheat", "Wheat", 2, 4, 0.45f, 24f, 3));
        registry.Register(new CropDefinition("carrot_crop", "Sweet Carrot", "carrot_seed", "Carrot Seeds", "carrot", "Fresh Carrot", 2, 3, 0.45f, 20f, 3));
        registry.Register(new CropDefinition("pumpkin_crop", "Giant Pumpkin", "pumpkin_seed", "Pumpkin Seeds", "pumpkin", "Harvest Pumpkin", 1, 2, 0.40f, 32f, 3));
        return registry;
    }
}
