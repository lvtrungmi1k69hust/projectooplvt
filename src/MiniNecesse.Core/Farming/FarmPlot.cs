using System;

namespace MiniNecesse.Core.Farming;

/// <summary>
/// Represents one tilled farmland tile capable of hosting a planted crop across growth stages.
/// </summary>
public sealed class FarmPlot
{
    public int TileCol { get; }
    public int TileRow { get; }
    public string? CropId { get; private set; }
    public float GrowthElapsedSeconds { get; private set; }
    public float GrowthDurationSeconds { get; private set; }
    public int MaxStages { get; private set; } = 3;

    public bool IsPlanted => !string.IsNullOrWhiteSpace(CropId);
    public bool IsMature => IsPlanted && GrowthDurationSeconds > 0f && GrowthElapsedSeconds >= GrowthDurationSeconds;

    public float GrowthProgress => !IsPlanted || GrowthDurationSeconds <= 0f
        ? 0f
        : global::System.Math.Clamp(GrowthElapsedSeconds / GrowthDurationSeconds, 0f, 1f);

    public int CurrentStage
    {
        get
        {
            if (!IsPlanted)
            {
                return 0;
            }

            if (IsMature)
            {
                return MaxStages - 1;
            }

            int stage = (int)MathF.Floor(GrowthProgress * (MaxStages - 1));
            return global::System.Math.Clamp(stage, 0, MaxStages - 2);
        }
    }

    public FarmPlot(int tileCol, int tileRow)
    {
        TileCol = tileCol;
        TileRow = tileRow;
    }

    public bool TryPlant(CropDefinition crop)
    {
        ArgumentNullException.ThrowIfNull(crop);
        if (IsPlanted)
        {
            return false;
        }

        CropId = crop.CropId;
        GrowthElapsedSeconds = 0f;
        GrowthDurationSeconds = crop.GrowthDurationSeconds;
        MaxStages = global::System.Math.Max(2, crop.GrowthStages);
        return true;
    }

    public void AdvanceGrowth(float deltaTime)
    {
        if (!IsPlanted || IsMature || deltaTime <= 0f)
        {
            return;
        }

        GrowthElapsedSeconds = global::System.Math.Min(GrowthDurationSeconds, GrowthElapsedSeconds + deltaTime);
    }

    public void ClearCrop()
    {
        CropId = null;
        GrowthElapsedSeconds = 0f;
        GrowthDurationSeconds = 0f;
    }

    public void RestoreState(string? cropId, float elapsedSeconds, float durationSeconds, int maxStages)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            ClearCrop();
            return;
        }

        CropId = cropId;
        GrowthDurationSeconds = global::System.Math.Max(1f, durationSeconds);
        GrowthElapsedSeconds = global::System.Math.Clamp(elapsedSeconds, 0f, GrowthDurationSeconds);
        MaxStages = global::System.Math.Max(2, maxStages);
    }
}
