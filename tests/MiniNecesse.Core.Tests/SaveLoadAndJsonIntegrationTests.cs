using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Tests.Mocks;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class SaveLoadAndJsonIntegrationTests
{
    [Fact]
    public void SaveAndLoad_GameStateDtoToTempJsonFile_RestoresPlayerInventoryStructuresAndEnemies()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"mininecesse_test_{Guid.NewGuid():N}");
        try
        {
            var storage = new JsonSaveStorage(tempDir);
            var itemFactory = ItemFactory.CreateDefault();
            var enemyFactory = EnemyFactory.CreateDefault();
            var level = new IslandLevel("ISLAND_01", "Greenwood Isle", "Forest", "KingSlime", 24, 18, 12, 9);
            var player = new Player("player_1", new Vector2D(320f, 256f), maxHp: 100);
            player.TakeDamage(25);
            player.Inventory.TryAddItem(itemFactory.ResolveItem("wood_axe"), 1);
            player.Inventory.TryAddItem(itemFactory.ResolveItem("wood"), 18);
            player.Inventory.TryAddItem(itemFactory.ResolveItem("copper_ore"), 7);
            player.SelectHotbarSlot(1);

            using var originalSession = new GameSession(level, player, itemFactory.ResolveItem);
            originalSession.Start();
            originalSession.RuleEngine.AddScore(420);
            originalSession.DayNightCycle.RestoreState(dayNumber: 2, elapsedInCycle: 35.5f);
            originalSession.AddResource(enemyFactory.CreateResourceNode(ResourceType.Tree, new Vector2D(128f, 160f)));
            originalSession.AddStructure(enemyFactory.CreateStructure(StructureType.Workbench, 10, 9, level.TileSize));
            Enemy slime = enemyFactory.CreateEnemy("Slime", new Vector2D(400f, 300f));
            slime.TakeDamage(10);
            originalSession.AddEnemy(slime);

            GameStateDTO savedDto = originalSession.CreateSnapshot();
            storage.Save(slotIndex: 1, savedDto);
            Assert.True(File.Exists(Path.Combine(tempDir, "save_slot_1.json")));

            GameStateDTO loadedDto = storage.Load(slotIndex: 1);
            var restoredLevel = new IslandLevel("ISLAND_01", "Greenwood Isle", "Forest", "KingSlime", 24, 18, 12, 9);
            var restoredPlayer = new Player("player_1", restoredLevel.PlayerSpawnWorldPosition, maxHp: 100);
            using var restoredSession = new GameSession(restoredLevel, restoredPlayer, itemFactory.ResolveItem);
            restoredSession.RestoreFromSnapshot(loadedDto, enemyFactory);

            Assert.Equal("ISLAND_01", loadedDto.IslandId);
            Assert.Equal(75, restoredSession.Player.CurrentHp);
            Assert.Equal(new Vector2D(320f, 256f), restoredSession.Player.Position);
            Assert.Equal(1, restoredSession.Player.SelectedHotbarIndex);
            Assert.Equal(18, restoredSession.Inventory.GetItemCount("wood"));
            Assert.Equal(7, restoredSession.Inventory.GetItemCount("copper_ore"));
            Assert.Equal(420, restoredSession.RuleEngine.Score);
            Assert.Equal(2, restoredSession.DayNightCycle.DayNumber);
            Assert.Single(restoredSession.Resources);
            Assert.Single(restoredSession.Structures);
            Assert.Single(restoredSession.Enemies);
            Assert.Equal(slime.CurrentHp, restoredSession.Enemies[0].CurrentHp);
            Assert.False(restoredLevel.IsTileWalkable(10, 9));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_CorruptedJsonSaveFile_ThrowsSaveCorruptedException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"mininecesse_corrupt_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "save_slot_1.json"), "{ \"islandId\": \"ISLAND_01\", BROKEN_JSON");

            var storage = new JsonSaveStorage(tempDir);

            SaveCorruptedException ex = Assert.Throws<SaveCorruptedException>(() => storage.Load(slotIndex: 1));
            Assert.Equal(1, ex.SlotIndex);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadIsland_AllFiveJsonIslandsConfigAndRecipes_LoadAndValidateSuccessfully()
    {
        var loader = new JsonLevelLoader();

        GameConfig config = loader.LoadConfig();
        Assert.Equal(32f, config.TileSize);
        Assert.True(config.PlayerBaseHp > 0);

        var recipes = loader.LoadRecipes();
        Assert.True(recipes.Count >= 13);

        var islandIds = loader.GetAvailableIslandIds();
        Assert.Equal(5, islandIds.Count);

        int previousBossHp = 0;
        foreach (string islandId in islandIds)
        {
            LoadedIslandPackage package = loader.LoadIsland(islandId);
            Assert.Equal(48, package.Level.Width);
            Assert.Equal(36, package.Level.Height);
            Assert.True(package.ResourceSpawns.Count >= 35);
            Assert.True(package.Waves.Count >= 3);
            Assert.True(package.Boss.MaxHp > previousBossHp);
            previousBossHp = package.Boss.MaxHp;
        }
    }

    [Fact]
    public void HeadlessSimulation_WithMockInputAndInMemoryStorage_MovesPlayerAndRoundTripsState()
    {
        var level = new IslandLevel("ISLAND_01", "Mock Isle", "Forest", "KingSlime", 16, 16, 8, 8);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);
        var itemFactory = ItemFactory.CreateDefault();
        var mockInput = new MockInputProvider { MovementAxis = new Vector2D(1f, 0f) };
        var memoryStorage = new InMemorySaveStorage();

        using var session = new GameSession(level, player, itemFactory.ResolveItem);
        session.Start();

        Vector2D initialPos = player.Position;
        var loop = new GameLoopController(dt =>
        {
            session.MovePlayer(mockInput.GetMovementAxis(), dt);
            session.Update(dt);
        });

        int steps = loop.StepFrame(0.10f);
        Assert.Equal(6, steps);
        Assert.True(player.Position.X > initialPos.X);

        memoryStorage.Save(1, session.CreateSnapshot());
        Assert.True(memoryStorage.Exists(1));
        GameStateDTO restored = memoryStorage.Load(1);
        Assert.Equal(player.Position.X, restored.Player.X, precision: 2);
    }
}
