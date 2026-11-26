using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.World;

/// <summary>
/// Data-driven biome definition used by <see cref="SeededEndlessLevelGenerator"/>
/// (loaded from <c>endless_biomes.json</c> with precomputed resource tables for zero-allocation chunk generation).
/// </summary>
public sealed class EndlessBiomeProfile
{
    private readonly List<(ResourceType Type, float CumulativeWeight)> _cumulativeResources = new();
    private readonly float _totalResourceWeight;

    public string BiomeId { get; }
    public string DisplayName { get; }
    public float MinTemperature { get; }
    public float MaxTemperature { get; }
    public float MinMoisture { get; }
    public float MaxMoisture { get; }
    public TileType PrimaryTile { get; }
    public TileType ShoreTile { get; }
    public TileType AccentTile { get; }
    public IReadOnlyDictionary<ResourceType, float> ResourceWeights { get; }
    public IReadOnlyList<string> EnemyArchetypes { get; }
    public string BossType { get; }

    public EndlessBiomeProfile(
        string biomeId,
        string displayName,
        float minTemperature,
        float maxTemperature,
        float minMoisture,
        float maxMoisture,
        TileType primaryTile,
        TileType shoreTile,
        TileType accentTile,
        IReadOnlyDictionary<ResourceType, float> resourceWeights,
        IReadOnlyList<string> enemyArchetypes,
        string bossType)
    {
        if (string.IsNullOrWhiteSpace(biomeId))
        {
            throw new ArgumentException("BiomeId cannot be empty.", nameof(biomeId));
        }

        ArgumentNullException.ThrowIfNull(resourceWeights);
        ArgumentNullException.ThrowIfNull(enemyArchetypes);

        BiomeId = biomeId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? BiomeId : displayName.Trim();
        MinTemperature = global::System.Math.Clamp(minTemperature, 0f, 1f);
        MaxTemperature = global::System.Math.Clamp(maxTemperature, MinTemperature, 1f);
        MinMoisture = global::System.Math.Clamp(minMoisture, 0f, 1f);
        MaxMoisture = global::System.Math.Clamp(maxMoisture, MinMoisture, 1f);
        PrimaryTile = primaryTile;
        ShoreTile = shoreTile;
        AccentTile = accentTile;
        ResourceWeights = resourceWeights;
        EnemyArchetypes = enemyArchetypes.Count > 0 ? enemyArchetypes : new[] { "Slime" };
        BossType = string.IsNullOrWhiteSpace(bossType) ? "KingSlime" : bossType.Trim();

        float sum = 0f;
        foreach (KeyValuePair<ResourceType, float> kvp in resourceWeights)
        {
            if (kvp.Value <= 0f)
            {
                continue;
            }

            sum += kvp.Value;
            _cumulativeResources.Add((kvp.Key, sum));
        }

        _totalResourceWeight = sum;
    }

    public bool Matches(float temperature, float moisture)
    {
        return temperature >= MinTemperature && temperature <= MaxTemperature &&
               moisture >= MinMoisture && moisture <= MaxMoisture;
    }

    public float DistanceSquaredToCenter(float temperature, float moisture)
    {
        float midT = (MinTemperature + MaxTemperature) * 0.5f;
        float midM = (MinMoisture + MaxMoisture) * 0.5f;
        float dt = temperature - midT;
        float dm = moisture - midM;
        return (dt * dt) + (dm * dm);
    }

    public ResourceType PickWeightedResource(Random rng)
    {
        if (_cumulativeResources.Count == 0 || _totalResourceWeight <= 0f)
        {
            return ResourceType.Tree;
        }

        float roll = (float)(rng.NextDouble() * _totalResourceWeight);
        for (int i = 0; i < _cumulativeResources.Count; i++)
        {
            if (roll <= _cumulativeResources[i].CumulativeWeight)
            {
                return _cumulativeResources[i].Type;
            }
        }

        return _cumulativeResources[^1].Type;
    }
}

/// <summary>
/// Registry of multi-biome Endless generation profiles loaded from <c>endless_biomes.json</c>.
/// </summary>
public sealed class EndlessBiomeCatalog
{
    private readonly List<EndlessBiomeProfile> _biomes = new();
    private readonly IReadOnlyList<EndlessBiomeProfile> _biomesView;

    public IReadOnlyList<EndlessBiomeProfile> Biomes => _biomesView;

    public EndlessBiomeCatalog()
    {
        _biomesView = _biomes.AsReadOnly();
    }

    public void Register(EndlessBiomeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        for (int i = 0; i < _biomes.Count; i++)
        {
            if (string.Equals(_biomes[i].BiomeId, profile.BiomeId, StringComparison.OrdinalIgnoreCase))
            {
                _biomes[i] = profile;
                return;
            }
        }

        _biomes.Add(profile);
    }

    public EndlessBiomeProfile ResolveBiome(float temperature, float moisture)
    {
        if (_biomes.Count == 0)
        {
            throw new InvalidOperationException("EndlessBiomeCatalog must contain at least one biome.");
        }

        for (int i = 0; i < _biomes.Count; i++)
        {
            if (_biomes[i].Matches(temperature, moisture))
            {
                return _biomes[i];
            }
        }

        EndlessBiomeProfile best = _biomes[0];
        float bestDistSq = best.DistanceSquaredToCenter(temperature, moisture);
        for (int i = 1; i < _biomes.Count; i++)
        {
            float distSq = _biomes[i].DistanceSquaredToCenter(temperature, moisture);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = _biomes[i];
            }
        }

        return best;
    }

    public static EndlessBiomeCatalog CreateDefault()
    {
        var catalog = new EndlessBiomeCatalog();

        catalog.Register(new EndlessBiomeProfile(
            "Forest",
            "Verdant Forest",
            0.28f, 0.70f, 0.35f, 0.72f,
            TileType.Grass, TileType.Sand, TileType.Stone,
            new Dictionary<ResourceType, float>
            {
                [ResourceType.Tree] = 0.36f,
                [ResourceType.StoneRock] = 0.16f,
                [ResourceType.CopperDeposit] = 0.16f,
                [ResourceType.CoalDeposit] = 0.10f,
                [ResourceType.BerryBush] = 0.12f,
                [ResourceType.WheatCrop] = 0.10f
            },
            new[] { "Slime", "ZombieRaider" },
            "ForestGuardian"));

        catalog.Register(new EndlessBiomeProfile(
            "Desert",
            "Sunlit Dunes",
            0.70f, 1.00f, 0.00f, 0.42f,
            TileType.Sand, TileType.Sand, TileType.Stone,
            new Dictionary<ResourceType, float>
            {
                [ResourceType.StoneRock] = 0.28f,
                [ResourceType.CopperDeposit] = 0.28f,
                [ResourceType.GoldDeposit] = 0.18f,
                [ResourceType.CoalDeposit] = 0.14f,
                [ResourceType.WheatCrop] = 0.06f,
                [ResourceType.Tree] = 0.06f
            },
            new[] { "SkeletonArcher", "ZombieRaider" },
            "KingSlime"));

        catalog.Register(new EndlessBiomeProfile(
            "Swamp",
            "Boggy Mire",
            0.35f, 0.75f, 0.72f, 1.00f,
            TileType.Swamp, TileType.Grass, TileType.Grass,
            new Dictionary<ResourceType, float>
            {
                [ResourceType.Tree] = 0.25f,
                [ResourceType.IronDeposit] = 0.22f,
                [ResourceType.CoalDeposit] = 0.15f,
                [ResourceType.BerryBush] = 0.15f,
                [ResourceType.CopperDeposit] = 0.13f,
                [ResourceType.CrystalCluster] = 0.10f
            },
            new[] { "SwampZombie", "Slime" },
            "SwampWitch"));

        catalog.Register(new EndlessBiomeProfile(
            "Tundra",
            "Frozen Tundra",
            0.00f, 0.28f, 0.00f, 1.00f,
            TileType.Snow, TileType.Stone, TileType.Stone,
            new Dictionary<ResourceType, float>
            {
                [ResourceType.Tree] = 0.18f,
                [ResourceType.StoneRock] = 0.18f,
                [ResourceType.IronDeposit] = 0.24f,
                [ResourceType.GoldDeposit] = 0.16f,
                [ResourceType.CoalDeposit] = 0.12f,
                [ResourceType.CrystalCluster] = 0.12f
            },
            new[] { "FrostRaider", "SkeletonArcher" },
            "CryoGolem"));

        catalog.Register(new EndlessBiomeProfile(
            "Volcano",
            "Obsidian Wastes",
            0.70f, 1.00f, 0.42f, 1.00f,
            TileType.DarkStone, TileType.Stone, TileType.Stone,
            new Dictionary<ResourceType, float>
            {
                [ResourceType.StoneRock] = 0.20f,
                [ResourceType.IronDeposit] = 0.24f,
                [ResourceType.GoldDeposit] = 0.24f,
                [ResourceType.CrystalCluster] = 0.18f,
                [ResourceType.CoalDeposit] = 0.14f
            },
            new[] { "OrcBrute", "DarkMage", "StoneGolem" },
            "ShadowLord"));

        return catalog;
    }
}
