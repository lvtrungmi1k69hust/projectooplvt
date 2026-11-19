using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Patterns.Factory;

/// <summary>
/// Data-driven blueprint describing an enemy or boss archetype loaded from configuration.
/// </summary>
public sealed record EnemyBlueprint(
    string ArchetypeId,
    string DisplayName,
    int BaseMaxHp,
    float BaseMoveSpeed,
    int BaseAttackDamage,
    float AttackRange,
    float AttackCooldown,
    int ScoreReward,
    string StrategyKey,
    bool IsBoss = false,
    string? EnrageStrategyKey = null,
    float EnrageThresholdRatio = 0.30f);

/// <summary>
/// Data-driven blueprint describing a harvestable natural resource node.
/// </summary>
public sealed record ResourceBlueprint(
    ResourceType ResourceType,
    int MaxHp,
    ToolType RequiredTool,
    string DropItemId,
    int DropQuantity);

/// <summary>
/// Data-driven blueprint describing a player-placeable world structure.
/// </summary>
public sealed record StructureBlueprint(
    StructureType StructureType,
    int MaxHp,
    bool IsSolid,
    bool IsCraftingStation = false,
    float LightRadius = 0f);

/// <summary>
/// Data-driven blueprint describing an item definition loaded from JSON configuration.
/// </summary>
public sealed record ItemBlueprint(
    string Id,
    string Name,
    ItemCategory Category,
    int MaxStackSize = 99,
    string Description = "",
    int BaseDamage = 0,
    float AttackRange = 0f,
    float BaseCooldown = 0.4f,
    bool IsRanged = false,
    float ProjectileSpeed = 0f,
    ToolType ToolType = ToolType.None,
    int HarvestPower = 0,
    float HarvestRange = 48f,
    int HealAmount = 0,
    StructureType StructureType = StructureType.WoodenWall);
