using System.Collections.Generic;

namespace MiniNecesse.Data.Schemas;

internal sealed class GameConfigJson
{
    public float FixedDeltaTime { get; set; }
    public float TileSize { get; set; }
    public float DayDurationSeconds { get; set; }
    public float NightDurationSeconds { get; set; }
    public int PlayerBaseHp { get; set; }
    public float PlayerBaseSpeed { get; set; }
    public int PlayerBaseDamage { get; set; }
    public int InventorySlots { get; set; }
    public int HotbarSlots { get; set; }
    public float WorkbenchInteractRadius { get; set; }
    public int ChunkSize { get; set; }
    public int DefaultViewDistanceChunks { get; set; }
    public int MinViewDistanceChunks { get; set; }
    public int MaxViewDistanceChunks { get; set; }
    public float PlayerMaxHunger { get; set; }
    public float HungerDrainPerSecond { get; set; }
    public float StarvationDamageIntervalSeconds { get; set; }
    public int StarvationDamageAmount { get; set; }
}

internal sealed class FoodJson
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int MaxStackSize { get; set; }
    public int HealAmount { get; set; }
    public float HungerRestore { get; set; }
    public string? BuffId { get; set; }
    public string? BuffName { get; set; }
    public float BuffDurationSeconds { get; set; }
    public float SpeedMultiplier { get; set; } = 1f;
    public int BonusDamage { get; set; }
    public int BonusDefense { get; set; }
    public float RegenPerSecond { get; set; }
}

internal sealed class CropJson
{
    public string? CropId { get; set; }
    public string? DisplayName { get; set; }
    public string? SeedItemId { get; set; }
    public string? SeedName { get; set; }
    public string? HarvestItemId { get; set; }
    public string? HarvestName { get; set; }
    public int MinYield { get; set; }
    public int MaxYield { get; set; }
    public float BonusSeedChance { get; set; }
    public float GrowthDurationSeconds { get; set; }
    public int GrowthStages { get; set; }
}

internal sealed class ArmorJson
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Slot { get; set; }
    public int Defense { get; set; }
    public int MaxHpBonus { get; set; }
    public string? Description { get; set; }
}

internal sealed class LootEntryJson
{
    public string? ItemId { get; set; }
    public float DropChance { get; set; }
    public int MinQuantity { get; set; }
    public int MaxQuantity { get; set; }
    public bool Guaranteed { get; set; }
}

internal sealed class LootTableJson
{
    public string? ArchetypeId { get; set; }
    public List<LootEntryJson>? Entries { get; set; }
}

internal sealed class SettlerCatalogJson
{
    public List<string>? Names { get; set; }
    public List<SettlerProfessionJson>? Professions { get; set; }
}

internal sealed class SettlerProfessionJson
{
    public string? ProfessionId { get; set; }
    public string? DisplayName { get; set; }
    public int MaxHp { get; set; }
    public float MoveSpeed { get; set; }
    public float WorkIntervalSeconds { get; set; }
    public string? RecruitCostItemId { get; set; }
    public int RecruitCostQuantity { get; set; }
    public List<SettlerProducedJson>? ProducedItems { get; set; }
    public List<SettlerTradeJson>? Trades { get; set; }
}

internal sealed class SettlerProducedJson
{
    public string? ItemId { get; set; }
    public int Quantity { get; set; }
}

internal sealed class SettlerTradeJson
{
    public string? OfferId { get; set; }
    public string? CostItemId { get; set; }
    public int CostQuantity { get; set; }
    public string? RewardItemId { get; set; }
    public int RewardQuantity { get; set; }
}

internal sealed class RecipeJson
{
    public string? RecipeId { get; set; }
    public string? OutputItemId { get; set; }
    public int OutputQuantity { get; set; }
    public bool RequiresWorkbench { get; set; }
    public List<RecipeIngredientJson>? Ingredients { get; set; }
}

internal sealed class RecipeIngredientJson
{
    public string? ItemId { get; set; }
    public int Quantity { get; set; }
}

internal sealed class IslandJson
{
    public string? IslandId { get; set; }
    public string? Name { get; set; }
    public string? Biome { get; set; }
    public string? GenerationMode { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Seed { get; set; }
    public float? ResourceDensity { get; set; }
    public Dictionary<string, float>? ResourceWeights { get; set; }
    public GridCoordJson? PlayerSpawn { get; set; }
    public List<string>? TileLayout { get; set; }
    public List<ResourceSpawnJson>? ResourceSpawns { get; set; }
    public List<WaveJson>? Waves { get; set; }
    public BossJson? Boss { get; set; }
}

internal sealed class GridCoordJson
{
    public int X { get; set; }
    public int Y { get; set; }
}

internal sealed class ResourceSpawnJson
{
    public string? Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

internal sealed class WaveJson
{
    public int WaveNumber { get; set; }
    public List<WaveEnemyJson>? Enemies { get; set; }
}

internal sealed class WaveEnemyJson
{
    public string? Type { get; set; }
    public int Count { get; set; }
}

internal sealed class BossJson
{
    public string? Type { get; set; }
    public string? DisplayName { get; set; }
    public int MaxHp { get; set; }
    public float MoveSpeed { get; set; }
    public int AttackDamage { get; set; }
    public float AttackRange { get; set; }
    public float AttackCooldown { get; set; }
    public int ScoreReward { get; set; }
    public string? InitialStrategy { get; set; }
    public string? EnrageStrategy { get; set; }
    public float EnrageThresholdRatio { get; set; }
}

internal sealed class EndlessBiomeJson
{
    public string? BiomeId { get; set; }
    public string? DisplayName { get; set; }
    public float MinTemperature { get; set; }
    public float MaxTemperature { get; set; }
    public float MinMoisture { get; set; }
    public float MaxMoisture { get; set; }
    public string? PrimaryTile { get; set; }
    public string? ShoreTile { get; set; }
    public string? AccentTile { get; set; }
    public Dictionary<string, float>? ResourceWeights { get; set; }
    public List<string>? EnemyArchetypes { get; set; }
    public string? BossType { get; set; }
}
