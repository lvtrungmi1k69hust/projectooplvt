using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.World;

/// <summary>
/// Immutable structure placement coordinate emitted for a generated chunk (e.g., village walls, fences, torches).
/// </summary>
public sealed record StructureSpawnPoint(
    StructureType StructureType,
    int TileCol,
    int TileRow);

/// <summary>
/// Immutable passive animal spawn coordinate emitted for a generated chunk.
/// </summary>
public sealed record AnimalSpawnPoint(
    AnimalType AnimalType,
    int TileCol,
    int TileRow);

/// <summary>
/// Represents a procedurally generated village settlement containing cobblestone paths,
/// cottages with workbenches and torches, wheat/berry farms, and fenced livestock pens.
/// </summary>
public sealed class VillageSite
{
    private readonly HashSet<GridPoint> _roadTiles;
    private readonly HashSet<GridPoint> _wellTiles;
    private readonly HashSet<GridPoint> _footPrintTiles;
    private readonly List<StructureSpawnPoint> _structures;
    private readonly IReadOnlyList<StructureSpawnPoint> _structuresView;
    private readonly List<ResourceSpawnPoint> _farmResources;
    private readonly IReadOnlyList<ResourceSpawnPoint> _farmResourcesView;
    private readonly List<AnimalSpawnPoint> _livestock;
    private readonly IReadOnlyList<AnimalSpawnPoint> _livestockView;

    /// <summary>Gets the unique identifier of this village.</summary>
    public string VillageId { get; }

    /// <summary>Gets the display name of this village.</summary>
    public string Name { get; }

    /// <summary>Gets the central plaza column index (always walkable stone).</summary>
    public int CenterCol { get; }

    /// <summary>Gets the central plaza row index (always walkable stone).</summary>
    public int CenterRow { get; }

    /// <summary>Gets the approximate radius of this village in tiles.</summary>
    public int RadiusTiles { get; }

    /// <summary>Gets the set of cobblestone plaza and avenue tiles.</summary>
    public IReadOnlySet<GridPoint> RoadTiles => _roadTiles;

    /// <summary>Gets the set of water well tiles inside the village plaza.</summary>
    public IReadOnlySet<GridPoint> WellTiles => _wellTiles;

    /// <summary>Gets the complete tile footprint reserved for this village.</summary>
    public IReadOnlySet<GridPoint> FootPrintTiles => _footPrintTiles;

    /// <summary>Gets all village structures (houses, workbenches, torches, fences).</summary>
    public IReadOnlyList<StructureSpawnPoint> Structures => _structuresView;

    /// <summary>Gets all farm crops and bushes planted inside the village.</summary>
    public IReadOnlyList<ResourceSpawnPoint> FarmResources => _farmResourcesView;

    /// <summary>Gets all farm livestock spawned inside the village pen.</summary>
    public IReadOnlyList<AnimalSpawnPoint> Livestock => _livestockView;

    /// <summary>Initializes a new instance of the <see cref="VillageSite"/> class.</summary>
    public VillageSite(
        string villageId,
        string name,
        int centerCol,
        int centerRow,
        int radiusTiles,
        HashSet<GridPoint> roadTiles,
        HashSet<GridPoint> wellTiles,
        HashSet<GridPoint> footPrintTiles,
        List<StructureSpawnPoint> structures,
        List<ResourceSpawnPoint> farmResources,
        List<AnimalSpawnPoint> livestock)
    {
        if (string.IsNullOrWhiteSpace(villageId))
        {
            throw new ArgumentException("Village id cannot be empty.", nameof(villageId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Village name cannot be empty.", nameof(name));
        }

        VillageId = villageId;
        Name = name;
        CenterCol = centerCol;
        CenterRow = centerRow;
        RadiusTiles = radiusTiles;
        _roadTiles = roadTiles ?? throw new ArgumentNullException(nameof(roadTiles));
        _wellTiles = wellTiles ?? throw new ArgumentNullException(nameof(wellTiles));
        _footPrintTiles = footPrintTiles ?? throw new ArgumentNullException(nameof(footPrintTiles));
        _structures = structures ?? throw new ArgumentNullException(nameof(structures));
        _farmResources = farmResources ?? throw new ArgumentNullException(nameof(farmResources));
        _livestock = livestock ?? throw new ArgumentNullException(nameof(livestock));
        _structuresView = _structures.AsReadOnly();
        _farmResourcesView = _farmResources.AsReadOnly();
        _livestockView = _livestock.AsReadOnly();
    }

    /// <summary>Checks whether tile (<paramref name="col"/>, <paramref name="row"/>) lies inside this village footprint.</summary>
    public bool ContainsTile(int col, int row) => _footPrintTiles.Contains(new GridPoint(col, row));

    /// <summary>Checks whether this village bounding box overlaps a chunk tile rectangle.</summary>
    public bool OverlapsChunk(int startCol, int startRow, int endCol, int endRow)
    {
        return CenterCol + RadiusTiles >= startCol && CenterCol - RadiusTiles < endCol &&
               CenterRow + RadiusTiles >= startRow && CenterRow - RadiusTiles < endRow;
    }

    /// <summary>Checks whether <paramref name="worldPosition"/> is inside or immediately beside this village.</summary>
    public bool IsNearWorldPosition(Vector2D worldPosition, float tileSize)
    {
        if (tileSize <= 0f)
        {
            return false;
        }

        float cx = (CenterCol + 0.5f) * tileSize;
        float cy = (CenterRow + 0.5f) * tileSize;
        float maxDist = (RadiusTiles + 2) * tileSize;
        float dx = worldPosition.X - cx;
        float dy = worldPosition.Y - cy;
        return ((dx * dx) + (dy * dy)) <= (maxDist * maxDist);
    }
}
