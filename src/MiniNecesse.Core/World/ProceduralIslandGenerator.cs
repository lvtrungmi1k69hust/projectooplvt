using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.World;

/// <summary>
/// Configuration parameters controlling procedural island resource density, weights, and spawn safety.
/// </summary>
public sealed class ProceduralIslandConfig
{
    /// <summary>Gets the fraction of walkable chunk tiles targeted for resource node spawning.</summary>
    public float ResourceDensity { get; }

    /// <summary>Gets relative selection weights for each <see cref="ResourceType"/>.</summary>
    public IReadOnlyDictionary<ResourceType, float> ResourceWeights { get; }

    /// <summary>Gets the guaranteed safe walkable radius in tiles around player spawn.</summary>
    public int SafeSpawnRadiusTiles { get; }

    /// <summary>Initializes a new instance of the <see cref="ProceduralIslandConfig"/> class.</summary>
    public ProceduralIslandConfig(
        float resourceDensity = 0.028f,
        IReadOnlyDictionary<ResourceType, float>? resourceWeights = null,
        int safeSpawnRadiusTiles = 6)
    {
        if (resourceDensity < 0f || resourceDensity > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(resourceDensity), "Resource density must be within [0, 1].");
        }

        if (safeSpawnRadiusTiles < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(safeSpawnRadiusTiles), "Safe spawn radius must be >= 1 tile.");
        }

        ResourceDensity = resourceDensity;
        ResourceWeights = resourceWeights ?? new Dictionary<ResourceType, float>();
        SafeSpawnRadiusTiles = safeSpawnRadiusTiles;
    }
}

/// <summary>
/// Deterministic seed-based procedural island terrain, village settlement, resource, and wildlife generator.
/// Uses explicit bitwise integer mixing (independent of process-randomized <see cref="HashCode"/>)
/// so any chunk generates identically regardless of visit order or process restart.
/// </summary>
public sealed class ProceduralIslandGenerator
{
    private const int OUTER_OCEAN_MARGIN = 3;
    private const int VILLAGE_FOOTPRINT_RADIUS = 10;

    private static readonly string[] VILLAGE_NAMES =
    {
        "Oakstead", "Riverwatch", "Briarwood", "Sunhaven", "Copperford",
        "Mossvale", "Frosthearth", "Duskhollow", "Willowbrook", "Stonebridge"
    };

    private static readonly AnimalType[] ALL_ANIMAL_TYPES =
    {
        AnimalType.Chicken,
        AnimalType.Sheep,
        AnimalType.Cow,
        AnimalType.Pig
    };

    private readonly int _width;
    private readonly int _height;
    private readonly int _spawnCol;
    private readonly int _spawnRow;
    private readonly string _biome;
    private readonly int _seed;
    private readonly ProceduralIslandConfig _config;
    private readonly List<(ResourceType Type, float CumulativeWeight)> _weightedTable;
    private readonly float _totalWeight;

    private readonly List<VillageSite> _villages = new();
    private readonly IReadOnlyList<VillageSite> _villagesView;
    private readonly HashSet<GridPoint> _villageRoadTiles = new();
    private readonly HashSet<GridPoint> _villageWellTiles = new();
    private readonly HashSet<GridPoint> _villageFootprintTiles = new();

    /// <summary>Gets all randomly generated village settlements on this island.</summary>
    public IReadOnlyList<VillageSite> Villages => _villagesView;

    /// <summary>Initializes a new instance of the <see cref="ProceduralIslandGenerator"/> class.</summary>
    public ProceduralIslandGenerator(
        int width,
        int height,
        int spawnCol,
        int spawnRow,
        string biome,
        int seed,
        ProceduralIslandConfig? config = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Island dimensions must be positive.");
        }

        _width = width;
        _height = height;
        _spawnCol = spawnCol;
        _spawnRow = spawnRow;
        _biome = string.IsNullOrWhiteSpace(biome) ? "Forest" : biome.Trim();
        _seed = seed;
        _config = config ?? new ProceduralIslandConfig();
        _villagesView = _villages.AsReadOnly();
        (_weightedTable, _totalWeight) = BuildWeightTable(_biome, _config.ResourceWeights);

        GenerateRandomVillages();
    }

    /// <summary>
    /// Deterministically computes the <see cref="TileType"/> at (<paramref name="col"/>, <paramref name="row"/>).
    /// Safe for parallel or out-of-order chunk generation.
    /// </summary>
    public TileType GetTileType(int col, int row)
    {
        if (col < OUTER_OCEAN_MARGIN || row < OUTER_OCEAN_MARGIN ||
            col >= _width - OUTER_OCEAN_MARGIN || row >= _height - OUTER_OCEAN_MARGIN)
        {
            return TileType.Water;
        }

        var point = new GridPoint(col, row);
        if (_villageWellTiles.Contains(point))
        {
            return TileType.Water;
        }

        if (_villageRoadTiles.Contains(point))
        {
            return TileType.Stone;
        }

        if (_villageFootprintTiles.Contains(point))
        {
            return GetPrimaryBiomeTile();
        }

        int dxSpawn = col - _spawnCol;
        int dySpawn = row - _spawnRow;
        int spawnDistSq = (dxSpawn * dxSpawn) + (dySpawn * dySpawn);
        int safeRadius = _config.SafeSpawnRadiusTiles;
        if (spawnDistSq <= safeRadius * safeRadius)
        {
            return GetPrimaryBiomeTile();
        }

        float elevation = ComputeBaseElevation(col, row, spawnDistSq, safeRadius);
        if (elevation <= 0.03f)
        {
            return TileType.Water;
        }

        if (elevation <= 0.10f)
        {
            return GetShoreBiomeTile();
        }

        float lakeNoise = Fbm(col * 0.055f, row * 0.055f, _seed + 307, octaves: 3);
        if (elevation > 0.22f && lakeNoise < 0.21f)
        {
            return TileType.Water;
        }

        if (elevation > 0.20f && lakeNoise < 0.245f)
        {
            return GetShoreBiomeTile();
        }

        float ridgeNoise = Fbm(col * 0.048f, row * 0.048f, _seed + 613, octaves: 3);
        return ResolveInteriorBiomeTile(ridgeNoise);
    }

    /// <summary>
    /// Deterministically emits village buildings, torches, workbenches, and animal pen fences inside <paramref name="chunkCoord"/>.
    /// </summary>
    public void GenerateStructuresForChunk(IslandLevel level, ChunkCoord chunkCoord, List<StructureSpawnPoint> output)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();

        if (!level.IsChunkInBounds(chunkCoord) || _villages.Count == 0)
        {
            return;
        }

        (int startCol, int startRow, int endCol, int endRow) = GetChunkTileBounds(level, chunkCoord);
        for (int i = 0; i < _villages.Count; i++)
        {
            VillageSite village = _villages[i];
            if (!VillageOverlapsBounds(village, startCol, startRow, endCol, endRow))
            {
                continue;
            }

            for (int s = 0; s < village.Structures.Count; s++)
            {
                StructureSpawnPoint pt = village.Structures[s];
                if (pt.TileCol >= startCol && pt.TileCol < endCol && pt.TileRow >= startRow && pt.TileRow < endRow)
                {
                    output.Add(pt);
                }
            }
        }
    }

    /// <summary>
    /// Deterministically populates natural <see cref="ResourceSpawnPoint"/> entries and village crops for a single loaded chunk.
    /// </summary>
    public void GenerateResourcesForChunk(IslandLevel level, ChunkCoord chunkCoord, List<ResourceSpawnPoint> output)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();

        if (!level.IsChunkInBounds(chunkCoord))
        {
            return;
        }

        (int startCol, int startRow, int endCol, int endRow) = GetChunkTileBounds(level, chunkCoord);
        var occupied = new HashSet<GridPoint>();

        for (int i = 0; i < _villages.Count; i++)
        {
            VillageSite village = _villages[i];
            if (!VillageOverlapsBounds(village, startCol, startRow, endCol, endRow))
            {
                continue;
            }

            for (int r = 0; r < village.FarmResources.Count; r++)
            {
                ResourceSpawnPoint farmPt = village.FarmResources[r];
                if (farmPt.TileCol >= startCol && farmPt.TileCol < endCol &&
                    farmPt.TileRow >= startRow && farmPt.TileRow < endRow &&
                    level.IsTileWalkable(farmPt.TileCol, farmPt.TileRow))
                {
                    output.Add(farmPt);
                    occupied.Add(new GridPoint(farmPt.TileCol, farmPt.TileRow));
                }
            }
        }

        if (_totalWeight <= 0f || _config.ResourceDensity <= 0f)
        {
            return;
        }

        uint chunkSeed = MixHash(chunkCoord.ChunkX, chunkCoord.ChunkY, _seed ^ 0x517CC1B7);
        var rng = new Random((int)(chunkSeed & 0x7FFFFFFFu));

        for (int row = startRow; row < endRow; row++)
        {
            for (int col = startCol; col < endCol; col++)
            {
                if (!CanPlaceResourceAt(level, col, row, occupied))
                {
                    continue;
                }

                if (rng.NextDouble() < _config.ResourceDensity)
                {
                    ResourceType chosen = PickWeightedResource(rng);
                    output.Add(new ResourceSpawnPoint(chosen, col, row));
                    MarkResourceSpacing(col, row, occupied);
                }
            }
        }

        ChunkCoord spawnChunk = ChunkCoord.FromTile(_spawnCol, _spawnRow, level.ChunkSize);
        if (chunkCoord == spawnChunk && output.Count < 4)
        {
            EnsureMinimumSpawnChunkResources(level, startCol, startRow, endCol, endRow, rng, occupied, output);
        }
    }

    /// <summary>
    /// Deterministically emits village livestock and wild animal flocks (Chicken, Sheep, Cow, Pig) for <paramref name="chunkCoord"/>.
    /// </summary>
    public void GenerateAnimalsForChunk(IslandLevel level, ChunkCoord chunkCoord, List<AnimalSpawnPoint> output)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();

        if (!level.IsChunkInBounds(chunkCoord))
        {
            return;
        }

        (int startCol, int startRow, int endCol, int endRow) = GetChunkTileBounds(level, chunkCoord);
        var usedTiles = new HashSet<GridPoint>();

        for (int i = 0; i < _villages.Count; i++)
        {
            VillageSite village = _villages[i];
            if (!VillageOverlapsBounds(village, startCol, startRow, endCol, endRow))
            {
                continue;
            }

            for (int a = 0; a < village.Livestock.Count; a++)
            {
                AnimalSpawnPoint ap = village.Livestock[a];
                if (ap.TileCol >= startCol && ap.TileCol < endCol &&
                    ap.TileRow >= startRow && ap.TileRow < endRow &&
                    level.IsTileWalkable(ap.TileCol, ap.TileRow))
                {
                    output.Add(ap);
                    usedTiles.Add(new GridPoint(ap.TileCol, ap.TileRow));
                }
            }
        }

        uint chunkSeed = MixHash(chunkCoord.ChunkX, chunkCoord.ChunkY, _seed ^ 0x3C6EF372);
        var rng = new Random((int)(chunkSeed & 0x7FFFFFFFu));
        ChunkCoord spawnChunk = ChunkCoord.FromTile(_spawnCol, _spawnRow, level.ChunkSize);

        if (chunkCoord == spawnChunk)
        {
            SpawnStarterAnimalsNearSpawn(level, startCol, startRow, endCol, endRow, rng, usedTiles, output);
            return;
        }

        if (rng.NextDouble() >= 0.28)
        {
            return;
        }

        int herdCount = rng.Next(2, 4);
        AnimalType species = ALL_ANIMAL_TYPES[rng.Next(ALL_ANIMAL_TYPES.Length)];
        int anchorCol = rng.Next(startCol, endCol);
        int anchorRow = rng.Next(startRow, endRow);

        for (int attempt = 0; attempt < 24 && output.Count < herdCount; attempt++)
        {
            int col = global::System.Math.Clamp(anchorCol + rng.Next(-3, 4), startCol, endCol - 1);
            int row = global::System.Math.Clamp(anchorRow + rng.Next(-3, 4), startRow, endRow - 1);
            var pt = new GridPoint(col, row);
            if (_villageFootprintTiles.Contains(pt) || usedTiles.Contains(pt) || !level.IsTileWalkable(col, row))
            {
                continue;
            }

            usedTiles.Add(pt);
            output.Add(new AnimalSpawnPoint(species, col, row));
        }
    }

    private void SpawnStarterAnimalsNearSpawn(
        IslandLevel level,
        int startCol,
        int startRow,
        int endCol,
        int endRow,
        Random rng,
        HashSet<GridPoint> usedTiles,
        List<AnimalSpawnPoint> output)
    {
        int targetCount = 3;
        for (int attempt = 0; attempt < 48 && output.Count < targetCount; attempt++)
        {
            int col = rng.Next(startCol, endCol);
            int row = rng.Next(startRow, endRow);
            int dx = col - _spawnCol;
            int dy = row - _spawnRow;
            int distSq = (dx * dx) + (dy * dy);
            if (distSq < 9 || distSq > 64)
            {
                continue;
            }

            var pt = new GridPoint(col, row);
            if (_villageFootprintTiles.Contains(pt) || usedTiles.Contains(pt) || !level.IsTileWalkable(col, row))
            {
                continue;
            }

            usedTiles.Add(pt);
            AnimalType species = ALL_ANIMAL_TYPES[output.Count % ALL_ANIMAL_TYPES.Length];
            output.Add(new AnimalSpawnPoint(species, col, row));
        }
    }

    private void GenerateRandomVillages()
    {
        if (_width < 80 || _height < 80)
        {
            return;
        }

        uint villageSeed = MixHash(_width, _height, _seed ^ 0x6A09E667);
        var rng = new Random((int)(villageSeed & 0x7FFFFFFFu));
        var centers = new List<GridPoint>(6);

        // 1. Starter village within 28..44 tiles of spawn so the player can discover one quickly.
        if (TryFindVillageCenter(rng, 28f, 44f, 0f, MathF.PI * 2f, minVillageSpacing: 46, minSpawnDist: 24, centers, out GridPoint starter))
        {
            centers.Add(starter);
        }

        // 2. Four regional villages distributed across quadrants around the island.
        for (int q = 0; q < 4; q++)
        {
            float minAngle = q * (MathF.PI * 0.5f);
            float maxAngle = minAngle + (MathF.PI * 0.5f);
            if (TryFindVillageCenter(rng, 58f, 108f, minAngle, maxAngle, minVillageSpacing: 48, minSpawnDist: 24, centers, out GridPoint regional))
            {
                centers.Add(regional);
            }
        }

        // 3. Fallback pass to guarantee 5 villages if any quadrant hit ocean.
        for (int extra = 0; extra < 4 && centers.Count < 5; extra++)
        {
            if (TryFindVillageCenter(rng, 45f, 96f, 0f, MathF.PI * 2f, minVillageSpacing: 42, minSpawnDist: 24, centers, out GridPoint fallback))
            {
                centers.Add(fallback);
            }
        }

        int nameOffset = rng.Next(VILLAGE_NAMES.Length);
        for (int i = 0; i < centers.Count; i++)
        {
            string villageId = $"village_{i + 1}";
            string name = VILLAGE_NAMES[(nameOffset + i) % VILLAGE_NAMES.Length];
            VillageSite site = BuildVillageSite(villageId, name, centers[i].Col, centers[i].Row, rng);
            RegisterVillage(site);
        }
    }

    private bool TryFindVillageCenter(
        Random rng,
        float minRadius,
        float maxRadius,
        float minAngle,
        float maxAngle,
        int minVillageSpacing,
        int minSpawnDist,
        List<GridPoint> existingCenters,
        out GridPoint chosen)
    {
        int margin = VILLAGE_FOOTPRINT_RADIUS + OUTER_OCEAN_MARGIN + 6;
        int minSpacingSq = minVillageSpacing * minVillageSpacing;
        int minSpawnSq = minSpawnDist * minSpawnDist;

        for (int attempt = 0; attempt < 48; attempt++)
        {
            float angle = minAngle + ((float)rng.NextDouble() * (maxAngle - minAngle));
            float radius = minRadius + ((float)rng.NextDouble() * (maxRadius - minRadius));
            int cx = _spawnCol + (int)MathF.Round(MathF.Cos(angle) * radius);
            int cy = _spawnRow + (int)MathF.Round(MathF.Sin(angle) * radius);

            if (cx < margin || cy < margin || cx >= _width - margin || cy >= _height - margin)
            {
                continue;
            }

            int dSpawnX = cx - _spawnCol;
            int dSpawnY = cy - _spawnRow;
            if ((dSpawnX * dSpawnX) + (dSpawnY * dSpawnY) < minSpawnSq)
            {
                continue;
            }

            if (!IsInlandVillageArea(cx, cy))
            {
                continue;
            }

            bool tooClose = false;
            for (int i = 0; i < existingCenters.Count; i++)
            {
                int dx = cx - existingCenters[i].Col;
                int dy = cy - existingCenters[i].Row;
                if ((dx * dx) + (dy * dy) < minSpacingSq)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                chosen = new GridPoint(cx, cy);
                return true;
            }
        }

        chosen = default;
        return false;
    }

    private bool IsInlandVillageArea(int cx, int cy)
    {
        int r = _config.SafeSpawnRadiusTiles;
        int[][] probes =
        {
            new[] { cx, cy },
            new[] { cx - 8, cy - 8 },
            new[] { cx + 8, cy - 8 },
            new[] { cx - 8, cy + 8 },
            new[] { cx + 8, cy + 8 }
        };

        for (int i = 0; i < probes.Length; i++)
        {
            int px = probes[i][0];
            int py = probes[i][1];
            int dx = px - _spawnCol;
            int dy = py - _spawnRow;
            float elev = ComputeBaseElevation(px, py, (dx * dx) + (dy * dy), r);
            if (elev <= 0.18f)
            {
                return false;
            }
        }

        return true;
    }

    private VillageSite BuildVillageSite(string villageId, string name, int cx, int cy, Random rng)
    {
        var footprint = new HashSet<GridPoint>();
        var roads = new HashSet<GridPoint>();
        var wells = new HashSet<GridPoint>();
        var structures = new List<StructureSpawnPoint>();
        var farmResources = new List<ResourceSpawnPoint>();
        var livestock = new List<AnimalSpawnPoint>();

        for (int dy = -VILLAGE_FOOTPRINT_RADIUS; dy <= VILLAGE_FOOTPRINT_RADIUS; dy++)
        {
            for (int dx = -VILLAGE_FOOTPRINT_RADIUS; dx <= VILLAGE_FOOTPRINT_RADIUS; dx++)
            {
                footprint.Add(new GridPoint(cx + dx, cy + dy));
            }
        }

        // Cobblestone plaza (5x5 centered at cx,cy) and main cross avenues.
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                roads.Add(new GridPoint(cx + dx, cy + dy));
            }
        }

        for (int d = -9; d <= 9; d++)
        {
            roads.Add(new GridPoint(cx + d, cy));
            roads.Add(new GridPoint(cx, cy + d));
        }

        // Off-center plaza water well so (cx, cy) itself remains walkable.
        wells.Add(new GridPoint(cx - 2, cy - 2));
        structures.Add(new StructureSpawnPoint(StructureType.Torch, cx + 2, cy - 2));
        structures.Add(new StructureSpawnPoint(StructureType.Torch, cx - 2, cy + 2));

        StructureType primaryWall = (_biome.Contains("Dark", StringComparison.OrdinalIgnoreCase) || rng.NextDouble() < 0.35)
            ? StructureType.StoneWall
            : StructureType.WoodenWall;
        StructureType secondaryWall = primaryWall == StructureType.StoneWall
            ? StructureType.WoodenWall
            : (rng.NextDouble() < 0.5 ? StructureType.StoneWall : StructureType.WoodenWall);

        // NW Quadrant: Craftsman Cottage (6x5 walls) with Workbench and Torch.
        AddCottage(structures, cx - 8, cy - 8, 6, 5, doorOffsetX: 2, primaryWall, includeWorkbench: true);

        // NE Quadrant: Villager House (6x5 walls) with Torch.
        AddCottage(structures, cx + 3, cy - 8, 6, 5, doorOffsetX: 2, secondaryWall, includeWorkbench: false);

        // SW Quadrant: Village Wheat Farm & Berry Garden.
        for (int row = cy + 4; row <= cy + 6; row++)
        {
            for (int col = cx - 7; col <= cx - 4; col++)
            {
                farmResources.Add(new ResourceSpawnPoint(ResourceType.WheatCrop, col, row));
            }
        }

        farmResources.Add(new ResourceSpawnPoint(ResourceType.BerryBush, cx - 8, cy + 3));
        farmResources.Add(new ResourceSpawnPoint(ResourceType.BerryBush, cx - 8, cy + 6));
        structures.Add(new StructureSpawnPoint(StructureType.Torch, cx - 3, cy + 3));

        // SE Quadrant: Fenced Animal Pen with 1-tile open gate and mixed livestock.
        AddFencedAnimalPen(structures, livestock, cx + 3, cy + 3, 7, 6);
        structures.Add(new StructureSpawnPoint(StructureType.Torch, cx + 3, cy + 2));

        return new VillageSite(
            villageId,
            name,
            cx,
            cy,
            VILLAGE_FOOTPRINT_RADIUS,
            roads,
            wells,
            footprint,
            structures,
            farmResources,
            livestock);
    }

    private static void AddCottage(
        List<StructureSpawnPoint> structures,
        int left,
        int top,
        int width,
        int height,
        int doorOffsetX,
        StructureType wallType,
        bool includeWorkbench)
    {
        int right = left + width - 1;
        int bottom = top + height - 1;

        for (int row = top; row <= bottom; row++)
        {
            for (int col = left; col <= right; col++)
            {
                bool isBorder = row == top || row == bottom || col == left || col == right;
                if (!isBorder)
                {
                    continue;
                }

                // 2-tile doorway on bottom wall facing the village road.
                bool isDoor = row == bottom && (col == left + doorOffsetX || col == left + doorOffsetX + 1);
                if (!isDoor)
                {
                    structures.Add(new StructureSpawnPoint(wallType, col, row));
                }
            }
        }

        if (includeWorkbench)
        {
            structures.Add(new StructureSpawnPoint(StructureType.Workbench, left + 1, top + 1));
        }

        structures.Add(new StructureSpawnPoint(StructureType.Torch, right - 1, top + 1));
    }

    private static void AddFencedAnimalPen(
        List<StructureSpawnPoint> structures,
        List<AnimalSpawnPoint> livestock,
        int left,
        int top,
        int width,
        int height)
    {
        int right = left + width - 1;
        int bottom = top + height - 1;
        int gateCol = left + 2;

        for (int row = top; row <= bottom; row++)
        {
            for (int col = left; col <= right; col++)
            {
                bool isBorder = row == top || row == bottom || col == left || col == right;
                if (!isBorder || (row == top && col == gateCol))
                {
                    continue;
                }

                structures.Add(new StructureSpawnPoint(StructureType.WoodenFence, col, row));
            }
        }

        livestock.Add(new AnimalSpawnPoint(AnimalType.Chicken, left + 1, top + 1));
        livestock.Add(new AnimalSpawnPoint(AnimalType.Sheep, right - 1, top + 1));
        livestock.Add(new AnimalSpawnPoint(AnimalType.Cow, left + 2, bottom - 1));
        livestock.Add(new AnimalSpawnPoint(AnimalType.Pig, right - 1, bottom - 1));
    }

    private void RegisterVillage(VillageSite site)
    {
        _villages.Add(site);
        foreach (GridPoint pt in site.RoadTiles) _villageRoadTiles.Add(pt);
        foreach (GridPoint pt in site.WellTiles) _villageWellTiles.Add(pt);
        foreach (GridPoint pt in site.FootPrintTiles) _villageFootprintTiles.Add(pt);
    }

    private static (int StartCol, int StartRow, int EndCol, int EndRow) GetChunkTileBounds(IslandLevel level, ChunkCoord chunkCoord)
    {
        int startCol = chunkCoord.ChunkX * level.ChunkSize;
        int startRow = chunkCoord.ChunkY * level.ChunkSize;
        int endCol = global::System.Math.Min(level.Width, startCol + level.ChunkSize);
        int endRow = global::System.Math.Min(level.Height, startRow + level.ChunkSize);
        return (startCol, startRow, endCol, endRow);
    }

    private static bool VillageOverlapsBounds(VillageSite village, int startCol, int startRow, int endCol, int endRow)
    {
        int r = village.RadiusTiles + 1;
        return village.CenterCol + r >= startCol && village.CenterCol - r < endCol &&
               village.CenterRow + r >= startRow && village.CenterRow - r < endRow;
    }

    private float ComputeBaseElevation(int col, int row, int spawnDistSq, int safeRadius)
    {
        float nx = (col - (_width * 0.5f)) / (_width * 0.46f);
        float ny = (row - (_height * 0.5f)) / (_height * 0.46f);
        float radialDist = MathF.Sqrt((nx * nx) + (ny * ny));

        float coastNoise = Fbm(col * 0.032f, row * 0.032f, _seed + 101, octaves: 3);
        float spawnProtect = MathF.Exp(-spawnDistSq / (float)(safeRadius * safeRadius * 6));
        return (1.0f - radialDist) + ((coastNoise - 0.5f) * 0.34f) + (spawnProtect * 0.35f);
    }

    private void EnsureMinimumSpawnChunkResources(
        IslandLevel level,
        int startCol,
        int startRow,
        int endCol,
        int endRow,
        Random rng,
        HashSet<GridPoint> occupied,
        List<ResourceSpawnPoint> output)
    {
        for (int row = startRow; row < endRow && output.Count < 4; row++)
        {
            for (int col = startCol; col < endCol && output.Count < 4; col++)
            {
                int dx = col - _spawnCol;
                int dy = row - _spawnRow;
                int distSq = (dx * dx) + (dy * dy);
                if (distSq < 9 || distSq > 36 || !CanPlaceResourceAt(level, col, row, occupied))
                {
                    continue;
                }

                ResourceType chosen = PickWeightedResource(rng);
                output.Add(new ResourceSpawnPoint(chosen, col, row));
                MarkResourceSpacing(col, row, occupied);
            }
        }
    }

    private bool CanPlaceResourceAt(IslandLevel level, int col, int row, HashSet<GridPoint> occupied)
    {
        if (global::System.Math.Abs(col - _spawnCol) <= 2 && global::System.Math.Abs(row - _spawnRow) <= 2)
        {
            return false;
        }

        var point = new GridPoint(col, row);
        if (_villageFootprintTiles.Contains(point) || occupied.Contains(point))
        {
            return false;
        }

        return level.IsTileWalkable(col, row);
    }

    private static void MarkResourceSpacing(int col, int row, HashSet<GridPoint> occupied)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                occupied.Add(new GridPoint(col + dc, row + dr));
            }
        }
    }

    private ResourceType PickWeightedResource(Random rng)
    {
        float roll = (float)(rng.NextDouble() * _totalWeight);
        for (int i = 0; i < _weightedTable.Count; i++)
        {
            if (roll <= _weightedTable[i].CumulativeWeight)
            {
                return _weightedTable[i].Type;
            }
        }

        return _weightedTable[^1].Type;
    }

    private TileType GetPrimaryBiomeTile()
    {
        if (_biome.Contains("Swamp", StringComparison.OrdinalIgnoreCase)) return TileType.Swamp;
        if (_biome.Contains("Snow", StringComparison.OrdinalIgnoreCase)) return TileType.Snow;
        if (_biome.Contains("Dark", StringComparison.OrdinalIgnoreCase)) return TileType.DarkStone;
        return TileType.Grass;
    }

    private TileType GetShoreBiomeTile()
    {
        if (_biome.Contains("Swamp", StringComparison.OrdinalIgnoreCase)) return TileType.Grass;
        if (_biome.Contains("Snow", StringComparison.OrdinalIgnoreCase)) return TileType.Stone;
        if (_biome.Contains("Dark", StringComparison.OrdinalIgnoreCase)) return TileType.Stone;
        return TileType.Sand;
    }

    private TileType ResolveInteriorBiomeTile(float ridgeNoise)
    {
        if (_biome.Contains("Copper", StringComparison.OrdinalIgnoreCase))
        {
            return ridgeNoise > 0.57f ? TileType.Stone : TileType.Grass;
        }

        if (_biome.Contains("Swamp", StringComparison.OrdinalIgnoreCase))
        {
            return ridgeNoise > 0.60f ? TileType.Grass : TileType.Swamp;
        }

        if (_biome.Contains("Snow", StringComparison.OrdinalIgnoreCase))
        {
            return ridgeNoise > 0.61f ? TileType.Stone : TileType.Snow;
        }

        if (_biome.Contains("Dark", StringComparison.OrdinalIgnoreCase))
        {
            return ridgeNoise > 0.58f ? TileType.Stone : TileType.DarkStone;
        }

        return ridgeNoise > 0.65f ? TileType.Stone : TileType.Grass;
    }

    private static (List<(ResourceType Type, float CumulativeWeight)> Table, float Total) BuildWeightTable(
        string biome,
        IReadOnlyDictionary<ResourceType, float> customWeights)
    {
        IReadOnlyDictionary<ResourceType, float> source = customWeights.Count > 0
            ? customWeights
            : GetDefaultBiomeWeights(biome);

        var table = new List<(ResourceType Type, float CumulativeWeight)>(source.Count);
        float sum = 0f;
        foreach (KeyValuePair<ResourceType, float> kvp in source)
        {
            if (kvp.Value <= 0f)
            {
                continue;
            }

            sum += kvp.Value;
            table.Add((kvp.Key, sum));
        }

        return (table, sum);
    }

    private static Dictionary<ResourceType, float> GetDefaultBiomeWeights(string biome)
    {
        if (biome.Contains("Copper", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<ResourceType, float>
            {
                [ResourceType.Tree] = 0.24f,
                [ResourceType.StoneRock] = 0.16f,
                [ResourceType.CopperDeposit] = 0.24f,
                [ResourceType.IronDeposit] = 0.14f,
                [ResourceType.CoalDeposit] = 0.10f,
                [ResourceType.BerryBush] = 0.06f,
                [ResourceType.WheatCrop] = 0.06f
            };
        }

        if (biome.Contains("Swamp", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<ResourceType, float>
            {
                [ResourceType.Tree] = 0.24f,
                [ResourceType.CopperDeposit] = 0.14f,
                [ResourceType.IronDeposit] = 0.22f,
                [ResourceType.GoldDeposit] = 0.12f,
                [ResourceType.CoalDeposit] = 0.10f,
                [ResourceType.BerryBush] = 0.10f,
                [ResourceType.CrystalCluster] = 0.08f
            };
        }

        if (biome.Contains("Snow", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<ResourceType, float>
            {
                [ResourceType.Tree] = 0.18f,
                [ResourceType.StoneRock] = 0.14f,
                [ResourceType.IronDeposit] = 0.22f,
                [ResourceType.GoldDeposit] = 0.18f,
                [ResourceType.CoalDeposit] = 0.12f,
                [ResourceType.CrystalCluster] = 0.12f,
                [ResourceType.BerryBush] = 0.04f
            };
        }

        if (biome.Contains("Dark", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<ResourceType, float>
            {
                [ResourceType.StoneRock] = 0.18f,
                [ResourceType.IronDeposit] = 0.24f,
                [ResourceType.GoldDeposit] = 0.24f,
                [ResourceType.CoalDeposit] = 0.14f,
                [ResourceType.CrystalCluster] = 0.16f,
                [ResourceType.Tree] = 0.04f
            };
        }

        return new Dictionary<ResourceType, float>
        {
            [ResourceType.Tree] = 0.36f,
            [ResourceType.StoneRock] = 0.16f,
            [ResourceType.CopperDeposit] = 0.16f,
            [ResourceType.CoalDeposit] = 0.10f,
            [ResourceType.BerryBush] = 0.12f,
            [ResourceType.WheatCrop] = 0.10f
        };
    }

    private static float Fbm(float x, float y, int seed, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += amplitude * ValueNoise2D(x * frequency, y * frequency, seed + (i * 1009));
            maxAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return maxAmplitude > 0f ? value / maxAmplitude : 0f;
    }

    private static float ValueNoise2D(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;

        float u = fx * fx * (3f - (2f * fx));
        float v = fy * fy * (3f - (2f * fy));

        float n00 = HashToUnitFloat(ix, iy, seed);
        float n10 = HashToUnitFloat(ix + 1, iy, seed);
        float n01 = HashToUnitFloat(ix, iy + 1, seed);
        float n11 = HashToUnitFloat(ix + 1, iy + 1, seed);

        float nx0 = n00 + ((n10 - n00) * u);
        float nx1 = n01 + ((n11 - n01) * u);
        return nx0 + ((nx1 - nx0) * v);
    }

    private static float HashToUnitFloat(int x, int y, int seed)
    {
        uint h = MixHash(x, y, seed);
        return (h & 0xFFFFu) / 65535f;
    }

    private static uint MixHash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = ((uint)seed * 0x9E3779B9u) ^ 0xA5A5A5A5u;
            h ^= (uint)x * 0x85EBCA6Bu;
            h = (h ^ (h >> 16)) * 0xC2B2AE35u;
            h ^= (uint)y * 0x27D4EB2Fu;
            h ^= h >> 15;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            return h;
        }
    }
}
