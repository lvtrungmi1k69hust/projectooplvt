using System;

namespace MiniNecesse.Core.Farming;

/// <summary>
/// Immutable data-driven crop definition loaded from <c>crops.json</c>.
/// </summary>
public sealed record CropDefinition
{
    public string CropId { get; }
    public string DisplayName { get; }
    public string SeedItemId { get; }
    public string SeedName { get; }
    public string HarvestItemId { get; }
    public string HarvestName { get; }
    public int MinYield { get; }
    public int MaxYield { get; }
    public float BonusSeedChance { get; }
    public float GrowthDurationSeconds { get; }
    public int GrowthStages { get; }

    public CropDefinition(
        string cropId,
        string displayName,
        string seedItemId,
        string seedName,
        string harvestItemId,
        string harvestName,
        int minYield,
        int maxYield,
        float bonusSeedChance,
        float growthDurationSeconds,
        int growthStages = 3)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new ArgumentException("CropId cannot be empty.", nameof(cropId));
        }

        if (string.IsNullOrWhiteSpace(seedItemId) || string.IsNullOrWhiteSpace(harvestItemId))
        {
            throw new ArgumentException("SeedItemId and HarvestItemId cannot be empty.", nameof(seedItemId));
        }

        if (minYield <= 0 || maxYield < minYield)
        {
            throw new ArgumentOutOfRangeException(nameof(minYield), "Crop yield bounds require 0 < minYield <= maxYield.");
        }

        if (bonusSeedChance < 0f || bonusSeedChance > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(bonusSeedChance), "Bonus seed chance must be within [0, 1].");
        }

        if (growthDurationSeconds <= 0f || growthStages <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(growthDurationSeconds), "Crop growth duration and stages must be positive.");
        }

        CropId = cropId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? cropId : displayName;
        SeedItemId = seedItemId;
        SeedName = string.IsNullOrWhiteSpace(seedName) ? seedItemId : seedName;
        HarvestItemId = harvestItemId;
        HarvestName = string.IsNullOrWhiteSpace(harvestName) ? harvestItemId : harvestName;
        MinYield = minYield;
        MaxYield = maxYield;
        BonusSeedChance = bonusSeedChance;
        GrowthDurationSeconds = growthDurationSeconds;
        GrowthStages = growthStages;
    }
}
