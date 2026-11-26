using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.World;

/// <summary>
/// Deterministic seed-based multi-biome procedural world generator for Endless Mode (<see cref="ILevelGenerator"/>).
/// Generates 384x384 chunk-streamed realms with temperature/moisture biome regions, seeded outposts,
/// resources, livestock, and escalating raid waves independently of <see cref="ILevelLoader"/>.
/// </summary>
public sealed class SeededEndlessLevelGenerator : ILevelGenerator
{
    private const int OUTER_OCEAN_MARGIN = 4;
    private const int OUTPOST_HALF_SIZE = 7;
    private const int ENDLESS_WAVE_COUNT = 12;

    private static readonly AnimalType[] LIVESTOCK_TYPES =
    {
        AnimalType.Chicken,
        AnimalType.Sheep,
        AnimalType.Cow,
        AnimalType.Pig
    };

    private static readonly string[] OUTPOST_NAMES =
    {
        "Horizon Outpost", "Driftwood Camp", "Starfall Bastion",
        "Copper Ridge", "Frostwatch", "Ashvale Keep"
    };

    private readonly EndlessBiomeCatalog _defaultCatalog;

    /// <summary>Initializes a new instance of the <see cref="SeededEndlessLevelGenerator"/> class.</summary>
    public SeededEndlessLevelGenerator(EndlessBiomeCatalog? defaultCatalog = null)
    {
        _defaultCatalog = defaultCatalog ?? EndlessBiomeCatalog.CreateDefault();
    }

    /// <summary>
    /// Deterministically resolves the biome at world tile (<paramref name="col"/>, <paramref name="row"/>) for <paramref name="seed"/>.
    /// </summary>
    public EndlessBiomeProfile GetBiomeAtTile(int col, int row, int seed, EndlessWorldOptions? options = null)
    {
        EndlessBiomeCatalog catalog = options?.BiomeCatalog ?? _defaultCatalog;
        float rawTemp = DeterministicNoise.Fbm(col * 0.011f, row * 0.011f, seed + 131, octaves: 3);
        float rawMoist = DeterministicNoise.Fbm(col * 0.011f, row * 0.011f, seed + 487, octaves: 3);
        float temp = DeterministicNoise.ContrastStretch(rawTemp, 2.35f);
        float moist = DeterministicNoise.ContrastStretch(rawMoist, 2.35f);
        return catalog.ResolveBiome(temp, moist);
    }

    /// <inheritdoc />
    public LoadedIslandPackage GenerateWorld(int seed, EndlessWorldOptions? options = null)
    {
        EndlessWorldOptions opts = ValidateOptions(options ?? new EndlessWorldOptions());
        EndlessBiomeCatalog catalog = opts.BiomeCatalog ?? _defaultCatalog;

        int width = opts.Width;
        int height = opts.Height;
        int spawnCol = width / 2;
        int spawnRow = height / 2;

        IReadOnlyList<VillageSite> villages = GenerateOutposts(width, height, spawnCol, spawnRow, seed, opts.SafeSpawnRadiusTiles);
        var roadTiles = new HashSet<GridPoint>();
        var wellTiles = new HashSet<GridPoint>();
        var footprintTiles = new HashSet<GridPoint>();
        for (int i = 0; i < villages.Count; i++)
        {
            foreach (GridPoint r in villages[i].RoadTiles) roadTiles.Add(r);
            foreach (GridPoint w in villages[i].WellTiles) wellTiles.Add(w);
            foreach (GridPoint f in villages[i].FootPrintTiles) footprintTiles.Add(f);
        }

        TileType TerrainGenerator(int col, int row) =>
            ComputeTileType(col, row, width, height, spawnCol, spawnRow, opts.SafeSpawnRadiusTiles, seed, catalog, roadTiles, wellTiles, footprintTiles);

        string worldBossType = PickEndlessBoss(seed, catalog);
        var level = new IslandLevel(
            islandId: $"ENDLESS_{seed}",
            name: $"Endless Realm (Seed {seed})",
            biome: "MultiBiome",
            bossType: worldBossType,
            width: width,
            height: height,
            spawnCol: spawnCol,
            spawnRow: spawnRow,
            tileSize: opts.TileSize,
            terrainGenerator: TerrainGenerator,
            chunkSize: opts.ChunkSize,
            lazyChunkLoading: true);

        IReadOnlyList<WaveDefinition> waves = BuildEndlessWaves(seed, catalog);
        BossConfiguration bossConfig = BuildEndlessBossConfig(worldBossType);

        return new LoadedIslandPackage(
            level,
            Array.Empty<ResourceSpawnPoint>(),
            waves,
            bossConfig,
            seed,
            chunkResourceGenerator: (coord, output) =>
                GenerateChunkResources(level, coord, seed, opts, catalog, villages, footprintTiles, output),
            chunkStructureGenerator: (coord, output) =>
                GenerateChunkStructures(level, coord, villages, output),
            chunkAnimalGenerator: (coord, output) =>
                GenerateChunkAnimals(level, coord, seed, spawnCol, spawnRow, villages, footprintTiles, output),
            villages: villages);
    }

    private static EndlessWorldOptions ValidateOptions(EndlessWorldOptions opts)
    {
        if (opts.Width < 32 || opts.Height < 32)
        {
            throw new ArgumentOutOfRangeException(nameof(opts), "Endless world dimensions must be >= 32x32.");
        }

        if (opts.ChunkSize < 8 || opts.TileSize <= 0f || opts.SafeSpawnRadiusTiles < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(opts), "Invalid Endless world chunk or tile options.");
        }

        return opts;
    }

    private static TileType ComputeTileType(
        int col,
        int row,
        int width,
        int height,
        int spawnCol,
        int spawnRow,
        int safeRadius,
        int seed,
        EndlessBiomeCatalog catalog,
        HashSet<GridPoint> roadTiles,
        HashSet<GridPoint> wellTiles,
        HashSet<GridPoint> footprintTiles)
    {
        if (col < OUTER_OCEAN_MARGIN || row < OUTER_OCEAN_MARGIN ||
            col >= width - OUTER_OCEAN_MARGIN || row >= height - OUTER_OCEAN_MARGIN)
        {
            return TileType.Water;
        }

        var pt = new GridPoint(col, row);
        if (wellTiles.Contains(pt)) return TileType.Water;
        if (roadTiles.Contains(pt)) return TileType.Stone;
        if (footprintTiles.Contains(pt)) return TileType.Grass;

        int dx = col - spawnCol;
        int dy = row - spawnRow;
        int distSq = (dx * dx) + (dy * dy);
        if (distSq <= safeRadius * safeRadius)
        {
            return TileType.Grass;
        }

        // Continental edge mask so world boundaries ease into sandy shores and ocean.
        float normX = ((col / (float)(width - 1)) * 2f) - 1f;
        float normY = ((row / (float)(height - 1)) * 2f) - 1f;
        float edgeFalloff = MathF.Max(MathF.Abs(normX), MathF.Abs(normY));
        float warp = (DeterministicNoise.Fbm(col * 0.025f, row * 0.025f, seed + 71, octaves: 2) - 0.5f) * 0.25f;
        float elev = 1.0f - MathF.Pow(MathF.Max(0f, edgeFalloff + warp), 2.4f);

        if (elev <= 0.08f) return TileType.Water;

        float rawTemp = DeterministicNoise.Fbm(col * 0.011f, row * 0.011f, seed + 131, octaves: 3);
        float rawMoist = DeterministicNoise.Fbm(col * 0.011f, row * 0.011f, seed + 487, octaves: 3);
        float temp = DeterministicNoise.ContrastStretch(rawTemp, 2.35f);
        float moist = DeterministicNoise.ContrastStretch(rawMoist, 2.35f);
        EndlessBiomeProfile biome = catalog.ResolveBiome(temp, moist);

        if (elev <= 0.16f) return biome.ShoreTile;

        float lakeNoise = DeterministicNoise.Fbm(col * 0.052f, row * 0.052f, seed + 307, octaves: 3);
        if (elev > 0.24f && lakeNoise < 0.20f)
        {
            return TileType.Water;
        }

        if (elev > 0.22f && lakeNoise < 0.24f)
        {
            return biome.ShoreTile;
        }

        float ridgeNoise = DeterministicNoise.Fbm(col * 0.046f, row * 0.046f, seed + 613, octaves: 3);
        return ridgeNoise > 0.63f ? biome.AccentTile : biome.PrimaryTile;
    }

    private void GenerateChunkResources(
        IslandLevel level,
        ChunkCoord coord,
        int seed,
        EndlessWorldOptions opts,
        EndlessBiomeCatalog catalog,
        IReadOnlyList<VillageSite> villages,
        HashSet<GridPoint> footprintTiles,
        List<ResourceSpawnPoint> output)
    {
        output.Clear();
        if (!level.IsChunkInBounds(coord) || opts.ResourceDensity <= 0f)
        {
            return;
        }

        (int startCol, int startRow, int endCol, int endRow) = GetChunkBounds(level, coord);
        var occupied = new HashSet<GridPoint>();

        for (int i = 0; i < villages.Count; i++)
        {
            VillageSite v = villages[i];
            if (!v.OverlapsChunk(startCol, startRow, endCol, endRow)) continue;
            for (int r = 0; r < v.FarmResources.Count; r++)
            {
                ResourceSpawnPoint fp = v.FarmResources[r];
                if (fp.TileCol >= startCol && fp.TileCol < endCol &&
                    fp.TileRow >= startRow && fp.TileRow < endRow &&
                    level.IsTileWalkable(fp.TileCol, fp.TileRow))
                {
                    output.Add(fp);
                    occupied.Add(new GridPoint(fp.TileCol, fp.TileRow));
                }
            }
        }

        uint chunkSeed = DeterministicNoise.MixHash(coord.ChunkX, coord.ChunkY, seed ^ 0x4E3D2C1B);
        var rng = new Random((int)(chunkSeed & 0x7FFFFFFFu));

        for (int row = startRow; row < endRow; row++)
        {
            for (int col = startCol; col < endCol; col++)
            {
                int dx = col - level.SpawnCol;
                int dy = row - level.SpawnRow;
                if ((dx * dx) + (dy * dy) <= 9) continue;

                var pt = new GridPoint(col, row);
                if (occupied.Contains(pt) || footprintTiles.Contains(pt) || !level.IsTileWalkable(col, row))
                {
                    continue;
                }

                if (rng.NextDouble() < opts.ResourceDensity)
                {
                    EndlessBiomeProfile biome = ResolveBiomeFast(col, row, seed, catalog);
                    ResourceType chosen = biome.PickWeightedResource(rng);
                    output.Add(new ResourceSpawnPoint(chosen, col, row));
                    MarkSpacing(col, row, occupied);
                }
            }
        }

        ChunkCoord spawnChunk = ChunkCoord.FromTile(level.SpawnCol, level.SpawnRow, level.ChunkSize);
        if (coord == spawnChunk && output.Count < 4)
        {
            for (int attempt = 0; attempt < 40 && output.Count < 4; attempt++)
            {
                int col = rng.Next(startCol, endCol);
                int row = rng.Next(startRow, endRow);
                var pt = new GridPoint(col, row);
                int dx = col - level.SpawnCol;
                int dy = row - level.SpawnRow;
                if ((dx * dx) + (dy * dy) <= 9 || occupied.Contains(pt) || !level.IsTileWalkable(col, row))
                {
                    continue;
                }

                ResourceType fallback = (attempt % 2 == 0) ? ResourceType.Tree : ResourceType.StoneRock;
                output.Add(new ResourceSpawnPoint(fallback, col, row));
                MarkSpacing(col, row, occupied);
            }
        }
    }

    private static void GenerateChunkStructures(
        IslandLevel level,
        ChunkCoord coord,
        IReadOnlyList<VillageSite> villages,
        List<StructureSpawnPoint> output)
    {
        output.Clear();
        if (!level.IsChunkInBounds(coord) || villages.Count == 0) return;

        (int startCol, int startRow, int endCol, int endRow) = GetChunkBounds(level, coord);
        for (int i = 0; i < villages.Count; i++)
        {
            VillageSite v = villages[i];
            if (!v.OverlapsChunk(startCol, startRow, endCol, endRow)) continue;
            for (int s = 0; s < v.Structures.Count; s++)
            {
                StructureSpawnPoint pt = v.Structures[s];
                if (pt.TileCol >= startCol && pt.TileCol < endCol && pt.TileRow >= startRow && pt.TileRow < endRow)
                {
                    output.Add(pt);
                }
            }
        }
    }

    private static void GenerateChunkAnimals(
        IslandLevel level,
        ChunkCoord coord,
        int seed,
        int spawnCol,
        int spawnRow,
        IReadOnlyList<VillageSite> villages,
        HashSet<GridPoint> footprintTiles,
        List<AnimalSpawnPoint> output)
    {
        output.Clear();
        if (!level.IsChunkInBounds(coord)) return;

        (int startCol, int startRow, int endCol, int endRow) = GetChunkBounds(level, coord);
        var used = new HashSet<GridPoint>();

        for (int i = 0; i < villages.Count; i++)
        {
            VillageSite v = villages[i];
            if (!v.OverlapsChunk(startCol, startRow, endCol, endRow)) continue;
            for (int a = 0; a < v.Livestock.Count; a++)
            {
                AnimalSpawnPoint ap = v.Livestock[a];
                if (ap.TileCol >= startCol && ap.TileCol < endCol &&
                    ap.TileRow >= startRow && ap.TileRow < endRow &&
                    level.IsTileWalkable(ap.TileCol, ap.TileRow))
                {
                    output.Add(ap);
                    used.Add(new GridPoint(ap.TileCol, ap.TileRow));
                }
            }
        }

        uint chunkSeed = DeterministicNoise.MixHash(coord.ChunkX, coord.ChunkY, seed ^ 0x2F9A6B3D);
        var rng = new Random((int)(chunkSeed & 0x7FFFFFFFu));
        ChunkCoord spawnChunk = ChunkCoord.FromTile(spawnCol, spawnRow, level.ChunkSize);
        if (coord != spawnChunk && rng.NextDouble() >= 0.25)
        {
            return;
        }

        AnimalType species = LIVESTOCK_TYPES[rng.Next(LIVESTOCK_TYPES.Length)];
        int anchorCol = coord == spawnChunk ? spawnCol + 3 : rng.Next(startCol, endCol);
        int anchorRow = coord == spawnChunk ? spawnRow + 2 : rng.Next(startRow, endRow);
        int targetCount = coord == spawnChunk ? 3 : rng.Next(2, 4);

        for (int attempt = 0; attempt < 28 && output.Count < targetCount; attempt++)
        {
            int c = global::System.Math.Clamp(anchorCol + rng.Next(-3, 4), startCol, endCol - 1);
            int r = global::System.Math.Clamp(anchorRow + rng.Next(-3, 4), startRow, endRow - 1);
            var pt = new GridPoint(c, r);
            if (footprintTiles.Contains(pt) || used.Contains(pt) || !level.IsTileWalkable(c, r))
            {
                continue;
            }

            used.Add(pt);
            output.Add(new AnimalSpawnPoint(species, c, r));
        }
    }

    private static IReadOnlyList<VillageSite> GenerateOutposts(
        int width,
        int height,
        int spawnCol,
        int spawnRow,
        int seed,
        int safeRadius)
    {
        var rng = new Random(seed ^ 0x19F03A71);
        int targetOutposts = global::System.Math.Clamp((width * height) / 36000, 1, 4);
        var list = new List<VillageSite>(targetOutposts);
        var centers = new List<GridPoint>(targetOutposts);

        int margin = 24;
        for (int attempt = 0; attempt < 120 && list.Count < targetOutposts; attempt++)
        {
            int cx = rng.Next(margin, width - margin);
            int cy = rng.Next(margin, height - margin);
            int dxSpawn = cx - spawnCol;
            int dySpawn = cy - spawnRow;
            int minDistFromSpawn = safeRadius + 20;
            if ((dxSpawn * dxSpawn) + (dySpawn * dySpawn) < minDistFromSpawn * minDistFromSpawn)
            {
                continue;
            }

            bool tooClose = false;
            for (int i = 0; i < centers.Count; i++)
            {
                int dx = cx - centers[i].Col;
                int dy = cy - centers[i].Row;
                if ((dx * dx) + (dy * dy) < 64 * 64)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            centers.Add(new GridPoint(cx, cy));
            int positiveSeed = unchecked((int)((uint)seed & 0x7FFFFFFFu));
            string name = OUTPOST_NAMES[(list.Count + positiveSeed) % OUTPOST_NAMES.Length];
            list.Add(BuildOutpostSite($"outpost_{list.Count + 1}", name, cx, cy, rng));
        }

        return list.AsReadOnly();
    }

    private static VillageSite BuildOutpostSite(string id, string name, int cx, int cy, Random rng)
    {
        var footprint = new HashSet<GridPoint>();
        var roads = new HashSet<GridPoint>();
        var wells = new HashSet<GridPoint>();
        var structures = new List<StructureSpawnPoint>();
        var farmResources = new List<ResourceSpawnPoint>();
        var livestock = new List<AnimalSpawnPoint>();

        for (int dy = -OUTPOST_HALF_SIZE; dy <= OUTPOST_HALF_SIZE; dy++)
        {
            for (int dx = -OUTPOST_HALF_SIZE; dx <= OUTPOST_HALF_SIZE; dx++)
            {
                footprint.Add(new GridPoint(cx + dx, cy + dy));
            }
        }

        for (int d = -5; d <= 5; d++)
        {
            roads.Add(new GridPoint(cx + d, cy));
            roads.Add(new GridPoint(cx, cy + d));
        }

        wells.Add(new GridPoint(cx - 2, cy - 2));
        structures.Add(new StructureSpawnPoint(StructureType.Torch, cx + 2, cy - 2));
        structures.Add(new StructureSpawnPoint(StructureType.Workbench, cx + 2, cy + 2));

        // 5x5 enclosed outpost cabin with a WoodenDoor and interior Torch (counts as valid settler house!).
        int left = cx - 6;
        int top = cy - 6;
        StructureType wallType = rng.NextDouble() < 0.5 ? StructureType.WoodenWall : StructureType.StoneWall;
        for (int r = top; r < top + 5; r++)
        {
            for (int c = left; c < left + 5; c++)
            {
                bool border = r == top || r == top + 4 || c == left || c == left + 4;
                if (!border) continue;
                bool isDoor = (r == top + 4) && (c == left + 2);
                structures.Add(new StructureSpawnPoint(isDoor ? StructureType.WoodenDoor : wallType, c, r));
            }
        }

        structures.Add(new StructureSpawnPoint(StructureType.Torch, left + 1, top + 1));

        for (int col = cx + 2; col <= cx + 5; col++)
        {
            farmResources.Add(new ResourceSpawnPoint(ResourceType.WheatCrop, col, cy - 5));
        }

        livestock.Add(new AnimalSpawnPoint(LIVESTOCK_TYPES[rng.Next(LIVESTOCK_TYPES.Length)], cx + 3, cy + 4));
        livestock.Add(new AnimalSpawnPoint(LIVESTOCK_TYPES[rng.Next(LIVESTOCK_TYPES.Length)], cx + 4, cy + 5));

        return new VillageSite(id, name, cx, cy, OUTPOST_HALF_SIZE, roads, wells, footprint, structures, farmResources, livestock);
    }

    private static string PickEndlessBoss(int seed, EndlessBiomeCatalog catalog)
    {
        IReadOnlyList<EndlessBiomeProfile> biomes = catalog.Biomes;
        int index = unchecked((int)((uint)seed & 0x7FFFFFFFu)) % biomes.Count;
        return biomes[index].BossType;
    }

    private static BossConfiguration BuildEndlessBossConfig(string bossType)
    {
        return new BossConfiguration(
            bossType,
            $"Endless {bossType}",
            MaxHp: 560,
            MoveSpeed: 88f,
            AttackDamage: 26,
            AttackRange: 170f,
            AttackCooldown: 1.05f,
            ScoreReward: 1000,
            InitialStrategyKey: "melee",
            EnrageStrategyKey: "boss_enrage_heavy",
            EnrageThresholdRatio: 0.45f);
    }

    private static IReadOnlyList<WaveDefinition> BuildEndlessWaves(int seed, EndlessBiomeCatalog catalog)
    {
        var archetypePool = new List<string>();
        for (int b = 0; b < catalog.Biomes.Count; b++)
        {
            IReadOnlyList<string> list = catalog.Biomes[b].EnemyArchetypes;
            for (int e = 0; e < list.Count; e++)
            {
                if (!archetypePool.Contains(list[e]))
                {
                    archetypePool.Add(list[e]);
                }
            }
        }

        if (archetypePool.Count == 0)
        {
            archetypePool.Add("Slime");
        }

        var rng = new Random(seed ^ 0x55A19C03);
        var waves = new List<WaveDefinition>(ENDLESS_WAVE_COUNT);
        for (int waveNum = 1; waveNum <= ENDLESS_WAVE_COUNT; waveNum++)
        {
            string primary = archetypePool[(waveNum + rng.Next(archetypePool.Count)) % archetypePool.Count];
            string secondary = archetypePool[(waveNum + 2) % archetypePool.Count];
            int primaryCount = 3 + waveNum;
            int secondaryCount = global::System.Math.Max(1, waveNum / 2);

            var entries = new List<WaveSpawnEntry>
            {
                new(primary, primaryCount)
            };
            if (!string.Equals(secondary, primary, StringComparison.Ordinal))
            {
                entries.Add(new WaveSpawnEntry(secondary, secondaryCount));
            }

            waves.Add(new WaveDefinition(waveNum, entries));
        }

        return waves.AsReadOnly();
    }

    private static EndlessBiomeProfile ResolveBiomeFast(int col, int row, int seed, EndlessBiomeCatalog catalog)
    {
        float rawTemp = DeterministicNoise.Fbm(col * 0.011f, row * 0.011f, seed + 131, octaves: 3);
        float rawMoist = DeterministicNoise.Fbm(col * 0.011f, row * 0.011f, seed + 487, octaves: 3);
        float temp = DeterministicNoise.ContrastStretch(rawTemp, 2.35f);
        float moist = DeterministicNoise.ContrastStretch(rawMoist, 2.35f);
        return catalog.ResolveBiome(temp, moist);
    }

    private static (int StartCol, int StartRow, int EndCol, int EndRow) GetChunkBounds(IslandLevel level, ChunkCoord coord)
    {
        int startCol = coord.ChunkX * level.ChunkSize;
        int startRow = coord.ChunkY * level.ChunkSize;
        int endCol = global::System.Math.Min(level.Width, startCol + level.ChunkSize);
        int endRow = global::System.Math.Min(level.Height, startRow + level.ChunkSize);
        return (startCol, startRow, endCol, endRow);
    }

    private static void MarkSpacing(int col, int row, HashSet<GridPoint> occupied)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                occupied.Add(new GridPoint(col + dc, row + dr));
            }
        }
    }
}
