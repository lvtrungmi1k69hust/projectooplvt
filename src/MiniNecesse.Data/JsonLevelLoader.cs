using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Farming;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Loot;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Settlement;
using MiniNecesse.Core.World;
using MiniNecesse.Data.Schemas;

namespace MiniNecesse.Data;

/// <summary>
/// Loads and strictly validates game configuration, crafting recipes, and island JSON files
/// from the relative <c>DataAssets/</c> directory. Implements <see cref="ILevelLoader"/>.
/// </summary>
public sealed class JsonLevelLoader : ILevelLoader
{
    private const string DEFAULT_ASSETS_FOLDER = "DataAssets";
    private const string CONFIG_FILE_NAME = "config.json";
    private const string RECIPES_FILE_NAME = "recipes.json";
    private const string FOODS_FILE_NAME = "foods.json";
    private const string CROPS_FILE_NAME = "crops.json";
    private const string ARMOR_FILE_NAME = "armor.json";
    private const string LOOT_TABLES_FILE_NAME = "loot_tables.json";
    private const string SETTLERS_FILE_NAME = "settlers.json";
    private const string ENDLESS_BIOMES_FILE_NAME = "endless_biomes.json";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Dictionary<char, TileType> TILE_GLYPH_MAP = new()
    {
        ['W'] = TileType.Water,
        ['S'] = TileType.Sand,
        ['G'] = TileType.Grass,
        ['R'] = TileType.Stone,
        ['M'] = TileType.Swamp,
        ['N'] = TileType.Snow,
        ['D'] = TileType.DarkStone
    };

    private readonly string _assetsDirectory;

    /// <summary>Gets the resolved relative directory containing JSON data assets.</summary>
    public string AssetsDirectory => _assetsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLevelLoader"/> class.
    /// Defaults to the relative <c>DataAssets</c> folder beside the application executable.
    /// </summary>
    /// <param name="assetsDirectory">Optional custom directory override (primarily for automated tests).</param>
    public JsonLevelLoader(string? assetsDirectory = null)
    {
        _assetsDirectory = string.IsNullOrWhiteSpace(assetsDirectory)
            ? Path.Combine(AppContext.BaseDirectory, DEFAULT_ASSETS_FOLDER)
            : assetsDirectory;
    }

    /// <inheritdoc />
    public GameConfig LoadConfig()
    {
        string path = Path.Combine(_assetsDirectory, CONFIG_FILE_NAME);
        GameConfigJson dto = ReadJsonOrThrow<GameConfigJson>(path, "CONFIG");
        ValidateConfig(dto);

        int chunkSize = dto.ChunkSize == 0 ? IslandLevel.DEFAULT_CHUNK_SIZE : dto.ChunkSize;
        int minView = dto.MinViewDistanceChunks == 0 ? ChunkManager.DEFAULT_MIN_VIEW_DISTANCE : dto.MinViewDistanceChunks;
        int maxView = dto.MaxViewDistanceChunks == 0 ? ChunkManager.DEFAULT_MAX_VIEW_DISTANCE : dto.MaxViewDistanceChunks;
        int defaultView = dto.DefaultViewDistanceChunks == 0 ? ChunkManager.DEFAULT_VIEW_DISTANCE : dto.DefaultViewDistanceChunks;
        float maxHunger = dto.PlayerMaxHunger <= 0f ? 100f : dto.PlayerMaxHunger;
        float hungerDrain = dto.HungerDrainPerSecond <= 0f ? 0.9f : dto.HungerDrainPerSecond;
        float starveInterval = dto.StarvationDamageIntervalSeconds <= 0f ? 2.5f : dto.StarvationDamageIntervalSeconds;
        int starveDamage = dto.StarvationDamageAmount <= 0 ? 3 : dto.StarvationDamageAmount;

        return new GameConfig(
            dto.FixedDeltaTime,
            dto.TileSize,
            dto.DayDurationSeconds,
            dto.NightDurationSeconds,
            dto.PlayerBaseHp,
            dto.PlayerBaseSpeed,
            dto.PlayerBaseDamage,
            dto.InventorySlots,
            dto.HotbarSlots,
            dto.WorkbenchInteractRadius,
            chunkSize,
            defaultView,
            minView,
            maxView,
            maxHunger,
            hungerDrain,
            starveInterval,
            starveDamage);
    }

    /// <summary>Loads data-driven food and temporary buff definitions from <c>foods.json</c>.</summary>
    public IReadOnlyList<FoodDefinition> LoadFoods()
    {
        string path = Path.Combine(_assetsDirectory, FOODS_FILE_NAME);
        if (!File.Exists(path))
        {
            return Array.Empty<FoodDefinition>();
        }

        List<FoodJson> rawList = ReadJsonOrThrow<List<FoodJson>>(path, "FOODS");
        var foods = new List<FoodDefinition>(rawList.Count);
        for (int i = 0; i < rawList.Count; i++)
        {
            FoodJson raw = rawList[i];
            if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.Name))
            {
                throw new InvalidLevelDataException($"Food entry at index {i} is missing 'id' or 'name'.", "FOODS");
            }

            IFoodBuffStrategy? buff = null;
            if (!string.IsNullOrWhiteSpace(raw.BuffId) && raw.BuffDurationSeconds > 0f)
            {
                buff = new StatFoodBuffStrategy(
                    raw.BuffId,
                    string.IsNullOrWhiteSpace(raw.BuffName) ? raw.BuffId : raw.BuffName,
                    raw.BuffDurationSeconds,
                    raw.SpeedMultiplier <= 0f ? 1f : raw.SpeedMultiplier,
                    global::System.Math.Max(0, raw.BonusDamage),
                    global::System.Math.Max(0, raw.BonusDefense),
                    MathF.Max(0f, raw.RegenPerSecond));
            }

            foods.Add(new FoodDefinition(
                raw.Id,
                raw.Name,
                raw.Description ?? string.Empty,
                raw.MaxStackSize <= 0 ? 20 : raw.MaxStackSize,
                global::System.Math.Max(0, raw.HealAmount),
                MathF.Max(0f, raw.HungerRestore),
                buff));
        }

        return foods.AsReadOnly();
    }

    /// <summary>Loads data-driven farming crop definitions from <c>crops.json</c>.</summary>
    public IReadOnlyList<CropDefinition> LoadCrops()
    {
        string path = Path.Combine(_assetsDirectory, CROPS_FILE_NAME);
        if (!File.Exists(path))
        {
            return Array.Empty<CropDefinition>();
        }

        List<CropJson> rawList = ReadJsonOrThrow<List<CropJson>>(path, "CROPS");
        var crops = new List<CropDefinition>(rawList.Count);
        for (int i = 0; i < rawList.Count; i++)
        {
            CropJson raw = rawList[i];
            if (string.IsNullOrWhiteSpace(raw.CropId) || string.IsNullOrWhiteSpace(raw.SeedItemId) || string.IsNullOrWhiteSpace(raw.HarvestItemId))
            {
                throw new InvalidLevelDataException($"Crop entry at index {i} is missing required fields.", "CROPS");
            }

            crops.Add(new CropDefinition(
                raw.CropId,
                raw.DisplayName ?? raw.CropId,
                raw.SeedItemId,
                raw.SeedName ?? raw.SeedItemId,
                raw.HarvestItemId,
                raw.HarvestName ?? raw.HarvestItemId,
                global::System.Math.Max(1, raw.MinYield),
                global::System.Math.Max(global::System.Math.Max(1, raw.MinYield), raw.MaxYield),
                global::System.Math.Clamp(raw.BonusSeedChance, 0f, 1f),
                raw.GrowthDurationSeconds <= 0f ? 20f : raw.GrowthDurationSeconds,
                raw.GrowthStages <= 0 ? 3 : raw.GrowthStages));
        }

        return crops.AsReadOnly();
    }

    /// <summary>Loads data-driven protective armor definitions from <c>armor.json</c>.</summary>
    public IReadOnlyList<ArmorDefinition> LoadArmorItems()
    {
        string path = Path.Combine(_assetsDirectory, ARMOR_FILE_NAME);
        if (!File.Exists(path))
        {
            return Array.Empty<ArmorDefinition>();
        }

        List<ArmorJson> rawList = ReadJsonOrThrow<List<ArmorJson>>(path, "ARMOR");
        var list = new List<ArmorDefinition>(rawList.Count);
        for (int i = 0; i < rawList.Count; i++)
        {
            ArmorJson raw = rawList[i];
            if (string.IsNullOrWhiteSpace(raw.Id) || string.IsNullOrWhiteSpace(raw.Name))
            {
                throw new InvalidLevelDataException($"Armor entry at index {i} is missing 'id' or 'name'.", "ARMOR");
            }

            if (!Enum.TryParse(raw.Slot, ignoreCase: true, out ArmorSlot slot))
            {
                throw new InvalidLevelDataException($"Armor '{raw.Id}' has invalid slot '{raw.Slot}'.", "ARMOR");
            }

            if (raw.Defense < 0 || raw.MaxHpBonus < 0)
            {
                throw new InvalidLevelDataException($"Armor '{raw.Id}' cannot have negative stats.", "ARMOR");
            }

            list.Add(new ArmorDefinition(
                raw.Id,
                raw.Name,
                slot,
                raw.Defense,
                raw.MaxHpBonus,
                raw.Description ?? string.Empty));
        }

        return list.AsReadOnly();
    }

    /// <summary>Loads data-driven enemy and boss loot tables from <c>loot_tables.json</c>.</summary>
    public IReadOnlyList<LootTable> LoadLootTables()
    {
        string path = Path.Combine(_assetsDirectory, LOOT_TABLES_FILE_NAME);
        if (!File.Exists(path))
        {
            return Array.Empty<LootTable>();
        }

        List<LootTableJson> rawList = ReadJsonOrThrow<List<LootTableJson>>(path, "LOOT_TABLES");
        var tables = new List<LootTable>(rawList.Count);
        for (int i = 0; i < rawList.Count; i++)
        {
            LootTableJson raw = rawList[i];
            if (string.IsNullOrWhiteSpace(raw.ArchetypeId) || raw.Entries is null)
            {
                throw new InvalidLevelDataException($"Loot table at index {i} is missing archetypeId or entries.", "LOOT_TABLES");
            }

            var entries = new List<LootEntry>(raw.Entries.Count);
            for (int j = 0; j < raw.Entries.Count; j++)
            {
                LootEntryJson e = raw.Entries[j];
                if (string.IsNullOrWhiteSpace(e.ItemId) || e.MinQuantity <= 0 || e.MaxQuantity < e.MinQuantity)
                {
                    throw new InvalidLevelDataException($"Invalid loot entry {j} in table '{raw.ArchetypeId}'.", "LOOT_TABLES");
                }

                entries.Add(new LootEntry(
                    e.ItemId,
                    global::System.Math.Clamp(e.DropChance, 0f, 1f),
                    e.MinQuantity,
                    e.MaxQuantity,
                    e.Guaranteed));
            }

            tables.Add(new LootTable(raw.ArchetypeId, entries.AsReadOnly()));
        }

        return tables.AsReadOnly();
    }

    /// <summary>Loads data-driven settler professions, recruitment costs, and barter offers from <c>settlers.json</c>.</summary>
    public SettlerCatalog LoadSettlerCatalog()
    {
        string path = Path.Combine(_assetsDirectory, SETTLERS_FILE_NAME);
        if (!File.Exists(path))
        {
            return SettlerCatalog.CreateDefault();
        }

        SettlerCatalogJson raw = ReadJsonOrThrow<SettlerCatalogJson>(path, "SETTLERS");
        var catalog = new SettlerCatalog();
        if (raw.Names is { Count: > 0 })
        {
            catalog.SetNames(raw.Names);
        }

        if (raw.Professions is null || raw.Professions.Count == 0)
        {
            throw new InvalidLevelDataException("settlers.json must define at least one profession.", "SETTLERS");
        }

        for (int i = 0; i < raw.Professions.Count; i++)
        {
            SettlerProfessionJson p = raw.Professions[i];
            if (string.IsNullOrWhiteSpace(p.ProfessionId) ||
                !Enum.TryParse(p.ProfessionId, ignoreCase: true, out SettlerProfession profession))
            {
                throw new InvalidLevelDataException($"Unknown settler profession '{p.ProfessionId}' at index {i}.", "SETTLERS");
            }

            if (p.MaxHp <= 0 || p.MoveSpeed <= 0f || p.WorkIntervalSeconds <= 0f ||
                string.IsNullOrWhiteSpace(p.RecruitCostItemId) || p.RecruitCostQuantity <= 0)
            {
                throw new InvalidLevelDataException($"Invalid settler stats/recruit cost for '{p.ProfessionId}'.", "SETTLERS");
            }

            var produced = new List<SettlerProducedEntry>();
            if (p.ProducedItems is not null)
            {
                for (int j = 0; j < p.ProducedItems.Count; j++)
                {
                    SettlerProducedJson pi = p.ProducedItems[j];
                    if (string.IsNullOrWhiteSpace(pi.ItemId) || pi.Quantity <= 0)
                    {
                        throw new InvalidLevelDataException($"Invalid produced item in '{p.ProfessionId}'.", "SETTLERS");
                    }

                    produced.Add(new SettlerProducedEntry(pi.ItemId, pi.Quantity));
                }
            }

            var trades = new List<SettlerTradeOffer>();
            if (p.Trades is not null)
            {
                for (int j = 0; j < p.Trades.Count; j++)
                {
                    SettlerTradeJson tr = p.Trades[j];
                    if (string.IsNullOrWhiteSpace(tr.OfferId) ||
                        string.IsNullOrWhiteSpace(tr.CostItemId) ||
                        string.IsNullOrWhiteSpace(tr.RewardItemId) ||
                        tr.CostQuantity <= 0 ||
                        tr.RewardQuantity <= 0)
                    {
                        throw new InvalidLevelDataException($"Invalid trade offer in '{p.ProfessionId}'.", "SETTLERS");
                    }

                    trades.Add(new SettlerTradeOffer(tr.OfferId, tr.CostItemId, tr.CostQuantity, tr.RewardItemId, tr.RewardQuantity));
                }
            }

            catalog.RegisterProfession(new SettlerProfessionDefinition(
                p.ProfessionId,
                profession,
                p.DisplayName ?? p.ProfessionId,
                p.MaxHp,
                p.MoveSpeed,
                p.WorkIntervalSeconds,
                p.RecruitCostItemId,
                p.RecruitCostQuantity,
                produced.AsReadOnly(),
                trades.AsReadOnly()));
        }

        return catalog;
    }

    /// <summary>Loads data-driven multi-biome Endless generation profiles from <c>endless_biomes.json</c>.</summary>
    public EndlessBiomeCatalog LoadEndlessBiomeCatalog()
    {
        string path = Path.Combine(_assetsDirectory, ENDLESS_BIOMES_FILE_NAME);
        if (!File.Exists(path))
        {
            return EndlessBiomeCatalog.CreateDefault();
        }

        List<EndlessBiomeJson> rawList = ReadJsonOrThrow<List<EndlessBiomeJson>>(path, "ENDLESS_BIOMES");
        if (rawList.Count == 0)
        {
            throw new InvalidLevelDataException("endless_biomes.json must define at least one biome.", "ENDLESS_BIOMES");
        }

        var catalog = new EndlessBiomeCatalog();
        for (int i = 0; i < rawList.Count; i++)
        {
            EndlessBiomeJson raw = rawList[i];
            if (string.IsNullOrWhiteSpace(raw.BiomeId) ||
                !Enum.TryParse(raw.PrimaryTile, ignoreCase: true, out TileType primary) ||
                !Enum.TryParse(raw.ShoreTile, ignoreCase: true, out TileType shore) ||
                !Enum.TryParse(raw.AccentTile, ignoreCase: true, out TileType accent))
            {
                throw new InvalidLevelDataException($"Invalid Endless biome tile definition at index {i}.", "ENDLESS_BIOMES");
            }

            var weights = new Dictionary<ResourceType, float>();
            if (raw.ResourceWeights is not null)
            {
                foreach (KeyValuePair<string, float> kvp in raw.ResourceWeights)
                {
                    if (Enum.TryParse(kvp.Key, ignoreCase: true, out ResourceType rt) && kvp.Value > 0f)
                    {
                        weights[rt] = kvp.Value;
                    }
                }
            }

            IReadOnlyList<string> enemies = raw.EnemyArchetypes is { Count: > 0 }
                ? raw.EnemyArchetypes.AsReadOnly()
                : new[] { "Slime" };

            catalog.Register(new EndlessBiomeProfile(
                raw.BiomeId,
                raw.DisplayName ?? raw.BiomeId,
                raw.MinTemperature,
                raw.MaxTemperature,
                raw.MinMoisture,
                raw.MaxMoisture,
                primary,
                shore,
                accent,
                weights,
                enemies,
                raw.BossType ?? "KingSlime"));
        }

        return catalog;
    }

    /// <inheritdoc />
    public IReadOnlyList<CraftingRecipe> LoadRecipes()
    {
        string path = Path.Combine(_assetsDirectory, RECIPES_FILE_NAME);
        List<RecipeJson> rawList = ReadJsonOrThrow<List<RecipeJson>>(path, "RECIPES");

        if (rawList.Count == 0)
        {
            throw new InvalidLevelDataException("Recipes file cannot be empty.", "RECIPES");
        }

        var recipes = new List<CraftingRecipe>(rawList.Count);
        for (int i = 0; i < rawList.Count; i++)
        {
            recipes.Add(ParseRecipe(rawList[i], i));
        }

        return recipes.AsReadOnly();
    }

    /// <inheritdoc />
    public LoadedIslandPackage LoadIsland(string islandId)
    {
        if (string.IsNullOrWhiteSpace(islandId))
        {
            throw new InvalidLevelDataException("Island identifier cannot be empty.");
        }

        string filePath = ResolveIslandFilePath(islandId);
        IslandJson raw = ReadJsonOrThrow<IslandJson>(filePath, islandId);
        bool isProcedural = string.Equals(raw.GenerationMode, "procedural", StringComparison.OrdinalIgnoreCase);
        ValidateIslandHeader(raw, islandId, isProcedural);

        GameConfig config = TryLoadConfigTileSize();
        IslandLevel level;
        Action<ChunkCoord, List<ResourceSpawnPoint>>? chunkResourceGenerator = null;
        Action<ChunkCoord, List<StructureSpawnPoint>>? chunkStructureGenerator = null;
        Action<ChunkCoord, List<AnimalSpawnPoint>>? chunkAnimalGenerator = null;
        IReadOnlyList<VillageSite> villages = Array.Empty<VillageSite>();

        if (isProcedural)
        {
            ProceduralIslandConfig procConfig = ParseProceduralConfig(raw);
            var generator = new ProceduralIslandGenerator(
                raw.Width,
                raw.Height,
                raw.PlayerSpawn!.X,
                raw.PlayerSpawn.Y,
                raw.Biome ?? "Forest",
                raw.Seed,
                procConfig);

            level = new IslandLevel(
                raw.IslandId!,
                raw.Name!,
                raw.Biome ?? "Forest",
                raw.Boss!.Type!,
                raw.Width,
                raw.Height,
                raw.PlayerSpawn.X,
                raw.PlayerSpawn.Y,
                config.TileSize,
                generator.GetTileType,
                config.ChunkSize,
                lazyChunkLoading: true);

            chunkResourceGenerator = (coord, output) => generator.GenerateResourcesForChunk(level, coord, output);
            chunkStructureGenerator = (coord, output) => generator.GenerateStructuresForChunk(level, coord, output);
            chunkAnimalGenerator = (coord, output) => generator.GenerateAnimalsForChunk(level, coord, output);
            villages = generator.Villages;
        }
        else
        {
            TileType[,] grid = ParseTileLayout(raw);
            level = new IslandLevel(
                raw.IslandId!,
                raw.Name!,
                raw.Biome ?? "Forest",
                raw.Boss!.Type!,
                raw.Width,
                raw.Height,
                raw.PlayerSpawn!.X,
                raw.PlayerSpawn.Y,
                config.TileSize,
                (col, row) => grid[row, col],
                config.ChunkSize,
                lazyChunkLoading: false);
        }

        IReadOnlyList<ResourceSpawnPoint> resources = ParseResourceSpawns(raw, level);
        IReadOnlyList<WaveDefinition> waves = ParseWaves(raw);
        BossConfiguration boss = ParseBoss(raw.Boss!, raw.IslandId!);

        return new LoadedIslandPackage(
            level,
            resources,
            waves,
            boss,
            raw.Seed,
            chunkResourceGenerator,
            chunkStructureGenerator,
            chunkAnimalGenerator,
            villages);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableIslandIds()
    {
        if (!Directory.Exists(_assetsDirectory))
        {
            return Array.Empty<string>();
        }

        string[] files = Directory.GetFiles(_assetsDirectory, "island_*.json");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        var ids = new List<string>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(files[i]);
            ids.Add(fileName.ToUpperInvariant());
        }

        return ids.AsReadOnly();
    }

    private T ReadJsonOrThrow<T>(string filePath, string contextId) where T : class
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidLevelDataException($"Required data file not found: '{filePath}'.", contextId);
        }

        try
        {
            string json = File.ReadAllText(filePath);
            T? parsed = JsonSerializer.Deserialize<T>(json, JSON_OPTIONS);
            if (parsed is null)
            {
                throw new InvalidLevelDataException($"JSON file '{filePath}' deserialized to null.", contextId);
            }

            return parsed;
        }
        catch (JsonException ex)
        {
            throw new InvalidLevelDataException($"Malformed JSON in '{filePath}': {ex.Message}", ex, contextId);
        }
        catch (IOException ex)
        {
            throw new InvalidLevelDataException($"Failed to read data file '{filePath}': {ex.Message}", ex, contextId);
        }
    }

    private string ResolveIslandFilePath(string islandId)
    {
        string normalized = islandId.Trim();
        string fileName = normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? normalized.ToLowerInvariant()
            : $"{normalized.ToLowerInvariant()}.json";

        return Path.Combine(_assetsDirectory, fileName);
    }

    private GameConfig TryLoadConfigTileSize()
    {
        string configPath = Path.Combine(_assetsDirectory, CONFIG_FILE_NAME);
        return File.Exists(configPath) ? LoadConfig() : new GameConfig();
    }

    private static void ValidateConfig(GameConfigJson dto)
    {
        if (dto.FixedDeltaTime <= 0f || dto.TileSize <= 0f ||
            dto.DayDurationSeconds <= 0f || dto.NightDurationSeconds <= 0f ||
            dto.PlayerBaseHp <= 0 || dto.PlayerBaseSpeed <= 0f ||
            dto.PlayerBaseDamage <= 0 || dto.InventorySlots <= 0 ||
            dto.HotbarSlots <= 0 || dto.WorkbenchInteractRadius <= 0f)
        {
            throw new InvalidLevelDataException(
                "config.json contains missing or non-positive game balance values.",
                "CONFIG");
        }

        if (dto.ChunkSize < 0 || dto.MinViewDistanceChunks < 0 || dto.MaxViewDistanceChunks < 0 || dto.DefaultViewDistanceChunks < 0)
        {
            throw new InvalidLevelDataException("config.json contains negative chunk streaming values.", "CONFIG");
        }

        int minView = dto.MinViewDistanceChunks == 0 ? ChunkManager.DEFAULT_MIN_VIEW_DISTANCE : dto.MinViewDistanceChunks;
        int maxView = dto.MaxViewDistanceChunks == 0 ? ChunkManager.DEFAULT_MAX_VIEW_DISTANCE : dto.MaxViewDistanceChunks;
        int defaultView = dto.DefaultViewDistanceChunks == 0 ? ChunkManager.DEFAULT_VIEW_DISTANCE : dto.DefaultViewDistanceChunks;

        if (minView > maxView || defaultView < minView || defaultView > maxView)
        {
            throw new InvalidLevelDataException(
                "config.json view distance bounds are inconsistent (require min <= default <= max).",
                "CONFIG");
        }
    }

    private static CraftingRecipe ParseRecipe(RecipeJson raw, int index)
    {
        if (string.IsNullOrWhiteSpace(raw.RecipeId) || string.IsNullOrWhiteSpace(raw.OutputItemId) || raw.OutputQuantity <= 0)
        {
            throw new InvalidLevelDataException($"Recipe at index {index} is missing required fields.", "RECIPES");
        }

        if (raw.Ingredients is null || raw.Ingredients.Count == 0)
        {
            throw new InvalidLevelDataException($"Recipe '{raw.RecipeId}' must define at least one ingredient.", "RECIPES");
        }

        var ingredients = new List<RecipeIngredient>(raw.Ingredients.Count);
        for (int i = 0; i < raw.Ingredients.Count; i++)
        {
            RecipeIngredientJson ing = raw.Ingredients[i];
            if (string.IsNullOrWhiteSpace(ing.ItemId) || ing.Quantity <= 0)
            {
                throw new InvalidLevelDataException($"Recipe '{raw.RecipeId}' has an invalid ingredient entry.", "RECIPES");
            }

            ingredients.Add(new RecipeIngredient(ing.ItemId, ing.Quantity));
        }

        return new CraftingRecipe(raw.RecipeId, raw.OutputItemId, raw.OutputQuantity, raw.RequiresWorkbench, ingredients);
    }

    private static void ValidateIslandHeader(IslandJson raw, string requestedId, bool isProcedural)
    {
        string id = raw.IslandId ?? requestedId;
        if (string.IsNullOrWhiteSpace(raw.IslandId) || string.IsNullOrWhiteSpace(raw.Name))
        {
            throw new InvalidLevelDataException("Island JSON must specify non-empty 'islandId' and 'name'.", id);
        }

        if (raw.Width <= 0 || raw.Height <= 0)
        {
            throw new InvalidLevelDataException($"Island dimensions {raw.Width}x{raw.Height} must be positive.", id);
        }

        if (raw.PlayerSpawn is null)
        {
            throw new InvalidLevelDataException("Island JSON is missing required 'playerSpawn' coordinates.", id);
        }

        if (!isProcedural && (raw.TileLayout is null || raw.TileLayout.Count != raw.Height))
        {
            throw new InvalidLevelDataException($"Island 'tileLayout' must contain exactly {raw.Height} rows.", id);
        }

        if (raw.Boss is null || string.IsNullOrWhiteSpace(raw.Boss.Type))
        {
            throw new InvalidLevelDataException("Island JSON is missing required 'boss' configuration.", id);
        }

        if (raw.ResourceDensity.HasValue && (raw.ResourceDensity.Value < 0f || raw.ResourceDensity.Value > 1f))
        {
            throw new InvalidLevelDataException("Island 'resourceDensity' must be within [0, 1].", id);
        }
    }

    private static ProceduralIslandConfig ParseProceduralConfig(IslandJson raw)
    {
        float density = raw.ResourceDensity ?? 0.028f;
        var weights = new Dictionary<ResourceType, float>();
        if (raw.ResourceWeights is not null)
        {
            foreach (KeyValuePair<string, float> kvp in raw.ResourceWeights)
            {
                if (!Enum.TryParse(kvp.Key, ignoreCase: true, out ResourceType resourceType))
                {
                    throw new InvalidLevelDataException($"Unknown procedural resourceWeight key '{kvp.Key}'.", raw.IslandId);
                }

                if (kvp.Value < 0f)
                {
                    throw new InvalidLevelDataException($"Negative procedural resourceWeight for '{kvp.Key}'.", raw.IslandId);
                }

                weights[resourceType] = kvp.Value;
            }
        }

        return new ProceduralIslandConfig(density, weights);
    }

    private static TileType[,] ParseTileLayout(IslandJson raw)
    {
        var grid = new TileType[raw.Height, raw.Width];
        for (int row = 0; row < raw.Height; row++)
        {
            string line = raw.TileLayout![row];
            if (string.IsNullOrEmpty(line) || line.Length != raw.Width)
            {
                throw new InvalidLevelDataException(
                    $"TileLayout row {row} must have length {raw.Width} (found {line?.Length ?? 0}).",
                    raw.IslandId);
            }

            for (int col = 0; col < raw.Width; col++)
            {
                char glyph = char.ToUpperInvariant(line[col]);
                if (!TILE_GLYPH_MAP.TryGetValue(glyph, out TileType tileType))
                {
                    throw new InvalidLevelDataException(
                        $"Unknown tile glyph '{glyph}' at ({col}, {row}).",
                        raw.IslandId);
                }

                grid[row, col] = tileType;
            }
        }

        return grid;
    }

    private static IReadOnlyList<ResourceSpawnPoint> ParseResourceSpawns(IslandJson raw, IslandLevel level)
    {
        if (raw.ResourceSpawns is null)
        {
            return Array.Empty<ResourceSpawnPoint>();
        }

        var list = new List<ResourceSpawnPoint>(raw.ResourceSpawns.Count);
        for (int i = 0; i < raw.ResourceSpawns.Count; i++)
        {
            ResourceSpawnJson item = raw.ResourceSpawns[i];
            if (string.IsNullOrWhiteSpace(item.Type) || !Enum.TryParse(item.Type, ignoreCase: true, out ResourceType resourceType))
            {
                throw new InvalidLevelDataException($"Unknown resource type '{item.Type}' at index {i}.", raw.IslandId);
            }

            if (!level.IsTileWalkable(item.X, item.Y))
            {
                throw new InvalidLevelDataException(
                    $"Resource '{item.Type}' at ({item.X}, {item.Y}) is outside walkable island bounds.",
                    raw.IslandId);
            }

            list.Add(new ResourceSpawnPoint(resourceType, item.X, item.Y));
        }

        return list.AsReadOnly();
    }

    private static IReadOnlyList<WaveDefinition> ParseWaves(IslandJson raw)
    {
        if (raw.Waves is null || raw.Waves.Count == 0)
        {
            throw new InvalidLevelDataException("Island JSON must define at least one raid wave.", raw.IslandId);
        }

        var waves = new List<WaveDefinition>(raw.Waves.Count);
        for (int i = 0; i < raw.Waves.Count; i++)
        {
            WaveJson w = raw.Waves[i];
            if (w.WaveNumber <= 0 || w.Enemies is null || w.Enemies.Count == 0)
            {
                throw new InvalidLevelDataException($"Wave at index {i} is invalid or has no enemies.", raw.IslandId);
            }

            List<WaveSpawnEntry> entries = w.Enemies
                .Select(e => ValidateWaveEnemy(e, raw.IslandId!))
                .ToList();

            waves.Add(new WaveDefinition(w.WaveNumber, entries));
        }

        return waves.AsReadOnly();
    }

    private static WaveSpawnEntry ValidateWaveEnemy(WaveEnemyJson enemy, string islandId)
    {
        if (string.IsNullOrWhiteSpace(enemy.Type) || enemy.Count <= 0)
        {
            throw new InvalidLevelDataException("Wave enemy entry must specify valid 'type' and positive 'count'.", islandId);
        }

        return new WaveSpawnEntry(enemy.Type, enemy.Count);
    }

    private static BossConfiguration ParseBoss(BossJson boss, string islandId)
    {
        if (string.IsNullOrWhiteSpace(boss.Type) || string.IsNullOrWhiteSpace(boss.DisplayName) ||
            boss.MaxHp <= 0 || boss.MoveSpeed <= 0f || boss.AttackDamage <= 0 ||
            boss.AttackRange <= 0f || boss.AttackCooldown <= 0f || boss.ScoreReward < 0 ||
            boss.EnrageThresholdRatio <= 0f || boss.EnrageThresholdRatio >= 1f)
        {
            throw new InvalidLevelDataException($"Boss configuration in '{islandId}' has invalid stats.", islandId);
        }

        return new BossConfiguration(
            boss.Type,
            boss.DisplayName,
            boss.MaxHp,
            boss.MoveSpeed,
            boss.AttackDamage,
            boss.AttackRange,
            boss.AttackCooldown,
            boss.ScoreReward,
            string.IsNullOrWhiteSpace(boss.InitialStrategy) ? "melee" : boss.InitialStrategy,
            string.IsNullOrWhiteSpace(boss.EnrageStrategy) ? "boss_enrage" : boss.EnrageStrategy,
            boss.EnrageThresholdRatio);
    }
}
