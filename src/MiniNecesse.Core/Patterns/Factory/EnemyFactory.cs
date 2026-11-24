using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Patterns.Factory;

/// <summary>
/// GoF Factory Method (Concrete Creator): creates <see cref="Enemy"/>, <see cref="Boss"/>,
/// <see cref="ResourceNode"/>, and <see cref="Structure"/> instances purely from registered blueprints
/// and strategy factories (zero hardcoded switch-case blocks).
/// </summary>
public sealed class EnemyFactory : IEntityFactory
{
    private static readonly Dictionary<Difficulty, (float HpMult, float DamageMult)> DIFFICULTY_SCALING = new()
    {
        [Difficulty.Easy] = (0.80f, 0.75f),
        [Difficulty.Normal] = (1.00f, 1.00f),
        [Difficulty.Hard] = (1.35f, 1.35f)
    };

    private readonly Dictionary<string, EnemyBlueprint> _enemyBlueprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IAIStrategy>> _strategyCreators = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ResourceType, ResourceBlueprint> _resourceBlueprints = new();
    private readonly Dictionary<StructureType, StructureBlueprint> _structureBlueprints = new();
    private int _entitySequence;

    /// <summary>Registers an enemy or boss archetype blueprint.</summary>
    public void RegisterEnemyBlueprint(EnemyBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        _enemyBlueprints[blueprint.ArchetypeId] = blueprint;
    }

    /// <summary>Registers a named AI strategy creator delegate.</summary>
    public void RegisterStrategyCreator(string strategyKey, Func<IAIStrategy> creator)
    {
        if (string.IsNullOrWhiteSpace(strategyKey))
        {
            throw new ArgumentException("Strategy key cannot be empty.", nameof(strategyKey));
        }

        ArgumentNullException.ThrowIfNull(creator);
        _strategyCreators[strategyKey] = creator;
    }

    /// <summary>Registers a natural resource node blueprint.</summary>
    public void RegisterResourceBlueprint(ResourceBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        _resourceBlueprints[blueprint.ResourceType] = blueprint;
    }

    /// <summary>Registers a placeable structure blueprint.</summary>
    public void RegisterStructureBlueprint(StructureBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        _structureBlueprints[blueprint.StructureType] = blueprint;
    }

    /// <inheritdoc />
    public Enemy CreateEnemy(string archetypeId, Vector2D spawnPosition, Difficulty difficulty = Difficulty.Normal)
    {
        EnemyBlueprint bp = ResolveEnemyBlueprint(archetypeId);
        (int scaledHp, int scaledDamage) = ScaleByDifficulty(bp.BaseMaxHp, bp.BaseAttackDamage, difficulty);
        IAIStrategy? strategy = ResolveStrategy(bp.StrategyKey);

        return new Enemy(
            NextId("enemy"),
            bp.ArchetypeId,
            spawnPosition,
            scaledHp,
            bp.BaseMoveSpeed,
            scaledDamage,
            bp.AttackRange,
            bp.AttackCooldown,
            bp.ScoreReward,
            strategy,
            lootDropItemId: bp.LootDropItemId,
            lootDropQuantity: bp.LootDropQuantity);
    }

    /// <inheritdoc />
    public Boss CreateBoss(string bossId, Vector2D spawnPosition, Difficulty difficulty = Difficulty.Normal)
    {
        EnemyBlueprint bp = ResolveEnemyBlueprint(bossId);
        (int scaledHp, int scaledDamage) = ScaleByDifficulty(bp.BaseMaxHp, bp.BaseAttackDamage, difficulty);
        IAIStrategy? initialStrategy = ResolveStrategy(bp.StrategyKey);
        IAIStrategy? enrageStrategy = string.IsNullOrWhiteSpace(bp.EnrageStrategyKey)
            ? null
            : ResolveStrategy(bp.EnrageStrategyKey);

        return new Boss(
            NextId("boss"),
            bp.DisplayName,
            spawnPosition,
            scaledHp,
            bp.BaseMoveSpeed,
            scaledDamage,
            bp.AttackRange,
            bp.AttackCooldown,
            bp.ScoreReward,
            initialStrategy,
            enrageStrategy,
            bp.EnrageThresholdRatio);
    }

    /// <inheritdoc />
    public ResourceNode CreateResourceNode(ResourceType resourceType, Vector2D position)
    {
        if (!_resourceBlueprints.TryGetValue(resourceType, out ResourceBlueprint? bp))
        {
            throw new KeyNotFoundException($"Resource blueprint '{resourceType}' is not registered.");
        }

        return new ResourceNode(NextId("res"), bp.ResourceType, position, bp.MaxHp, bp.RequiredTool, bp.DropItemId, bp.DropQuantity);
    }

    /// <inheritdoc />
    public Structure CreateStructure(StructureType structureType, int tileCol, int tileRow, float tileSize = 32f)
    {
        if (!_structureBlueprints.TryGetValue(structureType, out StructureBlueprint? bp))
        {
            throw new KeyNotFoundException($"Structure blueprint '{structureType}' is not registered.");
        }

        var worldPos = new Vector2D(tileCol * tileSize, tileRow * tileSize);
        return new Structure(
            NextId("str"),
            bp.StructureType,
            worldPos,
            tileCol,
            tileRow,
            bp.MaxHp,
            bp.IsSolid,
            bp.IsCraftingStation,
            bp.LightRadius,
            new Vector2D(tileSize, tileSize));
    }

    /// <summary>Creates an <see cref="EnemyFactory"/> preloaded with standard island enemies, bosses, resources, and structures.</summary>
    public static EnemyFactory CreateDefault(
        Action<Vector2D, Vector2D, int, string>? projectileSpawner = null,
        Func<GridPoint, Structure?>? structureLookup = null)
    {
        var factory = new EnemyFactory();
        factory.RegisterStrategyCreator("melee", () => new MeleeChaseStrategy(structureAtTileLookup: structureLookup));
        factory.RegisterStrategyCreator("ranged", () => new RangedKiteStrategy(projectileSpawner));
        factory.RegisterStrategyCreator("boss_enrage", () => new BossEnrageStrategy(projectileSpawner, volleyCount: 3));
        factory.RegisterStrategyCreator("boss_enrage_heavy", () => new BossEnrageStrategy(projectileSpawner, volleyCount: 5));

        RegisterDefaultEnemiesAndBosses(factory);
        RegisterDefaultResourcesAndStructures(factory);
        return factory;
    }

    private EnemyBlueprint ResolveEnemyBlueprint(string archetypeId)
    {
        if (_enemyBlueprints.TryGetValue(archetypeId, out EnemyBlueprint? bp))
        {
            return bp;
        }

        throw new KeyNotFoundException($"Enemy blueprint '{archetypeId}' is not registered in EnemyFactory.");
    }

    private IAIStrategy? ResolveStrategy(string strategyKey)
    {
        return _strategyCreators.TryGetValue(strategyKey, out Func<IAIStrategy>? creator)
            ? creator()
            : null;
    }

    private static (int Hp, int Damage) ScaleByDifficulty(int baseHp, int baseDamage, Difficulty difficulty)
    {
        (float hpMult, float dmgMult) = DIFFICULTY_SCALING.TryGetValue(difficulty, out var mult) ? mult : (1f, 1f);
        int hp = global::System.Math.Max(1, (int)MathF.Round(baseHp * hpMult));
        int damage = global::System.Math.Max(1, (int)MathF.Round(baseDamage * dmgMult));
        return (hp, damage);
    }

    private string NextId(string prefix)
    {
        _entitySequence++;
        return $"{prefix}_{_entitySequence}";
    }

    private static void RegisterDefaultEnemiesAndBosses(EnemyFactory factory)
    {
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("Slime", "Forest Slime", 35, 65f, 8, 28f, 1.1f, 50, "melee", LootDropItemId: "slime_gel", LootDropQuantity: 2));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("ZombieRaider", "Zombie Raider", 60, 75f, 12, 32f, 1.0f, 65, "melee", LootDropItemId: "slime_gel", LootDropQuantity: 1));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("SkeletonArcher", "Skeleton Archer", 45, 70f, 10, 185f, 1.4f, 75, "ranged", LootDropItemId: "wood", LootDropQuantity: 2));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("SwampZombie", "Swamp Zombie", 85, 72f, 15, 32f, 0.95f, 80, "melee", LootDropItemId: "slime_gel", LootDropQuantity: 2));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("FrostRaider", "Frost Raider", 105, 85f, 18, 34f, 0.9f, 95, "melee", LootDropItemId: "iron_ore", LootDropQuantity: 1));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("StoneGolem", "Stone Golem", 170, 54f, 22, 38f, 1.35f, 120, "melee", LootDropItemId: "copper_ore", LootDropQuantity: 2));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("OrcBrute", "Orc Brute", 145, 82f, 20, 36f, 0.95f, 110, "melee", LootDropItemId: "iron_ore", LootDropQuantity: 2));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("DarkMage", "Dark Mage", 115, 74f, 18, 220f, 1.20f, 130, "ranged", LootDropItemId: "gold_ore", LootDropQuantity: 2));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("KingSlime", "King Slime", 260, 72f, 16, 44f, 1.15f, 500, "melee", true, "boss_enrage"));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("ForestGuardian", "Forest Guardian", 340, 78f, 20, 48f, 1.1f, 550, "melee", true, "boss_enrage"));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("SwampWitch", "Swamp Witch", 390, 82f, 22, 190f, 1.2f, 600, "ranged", true, "boss_enrage"));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("CryoGolem", "Cryo Golem", 480, 68f, 26, 50f, 1.15f, 650, "melee", true, "boss_enrage_heavy"));
        factory.RegisterEnemyBlueprint(new EnemyBlueprint("ShadowLord", "Shadow Lord", 600, 92f, 30, 175f, 1.0f, 800, "ranged", true, "boss_enrage_heavy"));
    }

    private static void RegisterDefaultResourcesAndStructures(EnemyFactory factory)
    {
        factory.RegisterResourceBlueprint(new ResourceBlueprint(ResourceType.Tree, 30, ToolType.Axe, "wood", 3));
        factory.RegisterResourceBlueprint(new ResourceBlueprint(ResourceType.StoneRock, 40, ToolType.Pickaxe, "stone", 3));
        factory.RegisterResourceBlueprint(new ResourceBlueprint(ResourceType.CopperDeposit, 50, ToolType.Pickaxe, "copper_ore", 2));
        factory.RegisterResourceBlueprint(new ResourceBlueprint(ResourceType.IronDeposit, 70, ToolType.Pickaxe, "iron_ore", 2));
        factory.RegisterResourceBlueprint(new ResourceBlueprint(ResourceType.GoldDeposit, 90, ToolType.Pickaxe, "gold_ore", 2));

        factory.RegisterStructureBlueprint(new StructureBlueprint(StructureType.WoodenWall, 80, IsSolid: true));
        factory.RegisterStructureBlueprint(new StructureBlueprint(StructureType.StoneWall, 160, IsSolid: true));
        factory.RegisterStructureBlueprint(new StructureBlueprint(StructureType.Workbench, 100, IsSolid: true, IsCraftingStation: true));
        factory.RegisterStructureBlueprint(new StructureBlueprint(StructureType.Torch, 40, IsSolid: false, LightRadius: 150f));
    }
}
