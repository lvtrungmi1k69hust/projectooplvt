using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Farming;

/// <summary>
/// Coordinates farmland tilling, seed planting, crop growth updates, and mature harvests (SRP).
/// </summary>
public sealed class FarmingSystem
{
    private readonly CropRegistry _cropRegistry;
    private readonly List<FarmPlot> _plots = new();
    private readonly IReadOnlyList<FarmPlot> _plotsView;
    private readonly Dictionary<GridPoint, FarmPlot> _plotsByTile = new();

    /// <summary>Gets the active crop definition registry.</summary>
    public CropRegistry Registry => _cropRegistry;

    /// <summary>Gets a cached read-only list of all tilled farm plots.</summary>
    public IReadOnlyList<FarmPlot> Plots => _plotsView;

    /// <summary>Initializes a new instance of the <see cref="FarmingSystem"/> class.</summary>
    public FarmingSystem(CropRegistry? cropRegistry = null)
    {
        _cropRegistry = cropRegistry ?? CropRegistry.CreateDefault();
        _plotsView = _plots.AsReadOnly();
    }

    /// <summary>Returns the farm plot at (<paramref name="col"/>, <paramref name="row"/>), or <see langword="null"/>.</summary>
    public FarmPlot? GetPlotAt(int col, int row)
    {
        return _plotsByTile.TryGetValue(new GridPoint(col, row), out FarmPlot? plot) ? plot : null;
    }

    /// <summary>Tills a walkable Grass or Sand tile without blocking structures into farmland.</summary>
    public bool TryTillTile(IslandLevel level, int col, int row, Func<GridPoint, Structure?> structureLookup)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(structureLookup);

        var point = new GridPoint(col, row);
        if (_plotsByTile.ContainsKey(point) || !level.TryGetTile(col, row, out Tile? tile) || tile is null)
        {
            return false;
        }

        if (!tile.IsWalkable || (tile.TileType != TileType.Grass && tile.TileType != TileType.Sand))
        {
            return false;
        }

        if (structureLookup(point) is not null)
        {
            return false;
        }

        var plot = new FarmPlot(col, row);
        _plots.Add(plot);
        _plotsByTile[point] = plot;
        return true;
    }

    /// <summary>Plants a registered crop into an existing empty farm plot.</summary>
    public bool TryPlantSeed(int col, int row, string cropId)
    {
        if (!_cropRegistry.TryGetById(cropId, out CropDefinition? crop) || crop is null)
        {
            return false;
        }

        if (!_plotsByTile.TryGetValue(new GridPoint(col, row), out FarmPlot? plot))
        {
            return false;
        }

        return plot.TryPlant(crop);
    }

    /// <summary>Harvests a mature crop at (<paramref name="col"/>, <paramref name="row"/>), clearing the plot for replanting.</summary>
    public bool TryHarvestPlot(
        int col,
        int row,
        Random rng,
        out string harvestItemId,
        out int harvestQuantity,
        out string? bonusSeedItemId)
    {
        ArgumentNullException.ThrowIfNull(rng);
        harvestItemId = string.Empty;
        harvestQuantity = 0;
        bonusSeedItemId = null;

        if (!_plotsByTile.TryGetValue(new GridPoint(col, row), out FarmPlot? plot) || !plot.IsMature || plot.CropId is null)
        {
            return false;
        }

        if (!_cropRegistry.TryGetById(plot.CropId, out CropDefinition? crop) || crop is null)
        {
            plot.ClearCrop();
            return false;
        }

        harvestItemId = crop.HarvestItemId;
        harvestQuantity = crop.MinYield == crop.MaxYield
            ? crop.MinYield
            : rng.Next(crop.MinYield, crop.MaxYield + 1);

        if (crop.BonusSeedChance > 0f && rng.NextDouble() <= crop.BonusSeedChance)
        {
            bonusSeedItemId = crop.SeedItemId;
        }

        plot.ClearCrop();
        return true;
    }

    /// <summary>Advances growth of all planted plots with zero heap allocations.</summary>
    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < _plots.Count; i++)
        {
            FarmPlot plot = _plots[i];
            if (plot.IsPlanted && !plot.IsMature)
            {
                plot.AdvanceGrowth(deltaTime);
            }
        }
    }

    /// <summary>Restores a farm plot and any planted crop from save-state snapshot data.</summary>
    public void RestorePlot(int tileCol, int tileRow, string? cropId, float growthElapsedSeconds)
    {
        var point = new GridPoint(tileCol, tileRow);
        if (!_plotsByTile.TryGetValue(point, out FarmPlot? plot))
        {
            plot = new FarmPlot(tileCol, tileRow);
            _plots.Add(plot);
            _plotsByTile[point] = plot;
        }

        if (!string.IsNullOrWhiteSpace(cropId) && _cropRegistry.TryGetById(cropId, out CropDefinition? crop) && crop is not null)
        {
            plot.RestoreState(crop.CropId, growthElapsedSeconds, crop.GrowthDurationSeconds, crop.GrowthStages);
        }
        else
        {
            plot.ClearCrop();
        }
    }

    /// <summary>Clears all farm plots.</summary>
    public void Clear()
    {
        _plots.Clear();
        _plotsByTile.Clear();
    }
}
