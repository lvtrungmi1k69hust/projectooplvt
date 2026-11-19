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
    public int Width { get; set; }
    public int Height { get; set; }
    public int Seed { get; set; }
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
