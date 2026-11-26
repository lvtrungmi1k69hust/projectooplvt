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
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;
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
            maxView);
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
