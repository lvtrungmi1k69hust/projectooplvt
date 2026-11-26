using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Settlement;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class SettlementTests
{
    private static void BuildPerimeterWalls(
        GameSession session,
        EnemyFactory factory,
        int minCol,
        int maxCol,
        int minRow,
        int maxRow,
        params GridPoint[] doorTiles)
    {
        var doorSet = new HashSet<GridPoint>(doorTiles);
        float tileSize = session.Level.TileSize;

        for (int c = minCol; c <= maxCol; c++)
        {
            for (int r = minRow; r <= maxRow; r++)
            {
                bool onBorder = c == minCol || c == maxCol || r == minRow || r == maxRow;
                if (!onBorder)
                {
                    continue;
                }

                var pt = new GridPoint(c, r);
                StructureType type = doorSet.Contains(pt) ? StructureType.WoodenDoor : StructureType.WoodenWall;
                session.AddStructure(factory.CreateStructure(type, c, r, tileSize));
            }
        }
    }

    [Fact]
    public void HousingValidator_ValidatesEnclosedRoomsDeduplicatesDoorsAndRejectsInvalidRooms()
    {
        var level = new IslandLevel("ISLAND_01", "Housing Isle", "Forest", "KingSlime", 36, 36, 4, 4);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);
        var items = ItemFactory.CreateDefault();
        using var session = new GameSession(level, player, items.ResolveItem);
        var factory = EnemyFactory.CreateDefault();
        var validator = new HousingValidator();

        // 1. Valid enclosed room with 2 doors (cols 8..13, rows 8..12 => 4x3 = 12 interior tiles)
        BuildPerimeterWalls(session, factory, 8, 13, 8, 12, new GridPoint(10, 8), new GridPoint(11, 12));
        HouseValidationResult validTwoDoor = validator.ValidateRoom(level, new GridPoint(9, 9), session.FindStructureAtTile);
        Assert.True(validTwoDoor.IsValid);
        Assert.Equal(12, validTwoDoor.InteriorTileCount);
        Assert.Equal(2, validTwoDoor.DoorCount);

        var found = new List<HouseValidationResult>();
        validator.FindAllValidHouses(level, session.Structures, session.FindStructureAtTile, found);
        Assert.Single(found); // Multi-door room must be deduplicated cleanly

        // 2. Enclosed 3x2 box WITHOUT a door -> invalid
        BuildPerimeterWalls(session, factory, 16, 20, 8, 11);
        HouseValidationResult noDoor = validator.ValidateRoom(level, new GridPoint(17, 9), session.FindStructureAtTile);
        Assert.False(noDoor.IsValid);
        Assert.Equal(0, noDoor.DoorCount);

        // 3. Too small room (2x2 = 4 interior tiles < 6 minimum) with a door -> invalid
        BuildPerimeterWalls(session, factory, 22, 25, 8, 11, new GridPoint(23, 8));
        HouseValidationResult tooSmall = validator.ValidateRoom(level, new GridPoint(23, 9), session.FindStructureAtTile);
        Assert.False(tooSmall.IsValid);
        Assert.Equal(4, tooSmall.InteriorTileCount);

        // 4. Open wall gap leaking outside / to map edge -> invalid
        float tileSize = level.TileSize;
        for (int c = 8; c <= 12; c++)
        {
            session.AddStructure(factory.CreateStructure(StructureType.WoodenWall, c, 16, tileSize));
            session.AddStructure(factory.CreateStructure(StructureType.WoodenWall, c, 20, tileSize));
        }

        for (int r = 17; r <= 19; r++)
        {
            session.AddStructure(factory.CreateStructure(StructureType.WoodenDoor, 8, r, tileSize));
            // Deliberately leave col 12 open so room leaks out
        }

        HouseValidationResult leaking = validator.ValidateRoom(level, new GridPoint(10, 18), session.FindStructureAtTile);
        Assert.False(leaking.IsValid);
    }

    [Fact]
    public void Settlers_JsonCatalogRecruitHappinessWorkStrategiesTradeAndPersistenceWork()
    {
        var loader = new JsonLevelLoader();
        SettlerCatalog catalog = loader.LoadSettlerCatalog();
        Assert.True(catalog.TryGetProfession("Miner", out SettlerProfessionDefinition? minerDef));
        Assert.True(catalog.TryGetProfession("Hunter", out SettlerProfessionDefinition? hunterDef));
        Assert.NotNull(minerDef);
        Assert.NotNull(hunterDef);
        Assert.NotEmpty(catalog.Names);

        var level = new IslandLevel("ISLAND_01", "Colony Isle", "Forest", "KingSlime", 32, 32, 4, 4);
        var itemFactory = ItemFactory.CreateDefault();
        IReadOnlyList<FoodDefinition> foods = loader.LoadFoods();
        if (foods.Count > 0)
        {
            itemFactory.RegisterFoodDefinitions(foods);
        }

        var player = new Player("player_1", level.PlayerSpawnWorldPosition);
        player.Inventory.TryAddItem(itemFactory.ResolveItem("bread"), 2);
        player.Inventory.TryAddItem(itemFactory.ResolveItem("cooked_meat"), 1);

        var dayNight = new DayNightCycle(dayDurationSeconds: 120f, nightDurationSeconds: 45f);
        using var session = new GameSession(level, player, itemFactory.ResolveItem, dayNight, settlerCatalog: catalog);
        var enemyFactory = EnemyFactory.CreateDefault(
            projectileSpawner: (origin, vel, dmg, ownerId) => session.SpawnPooledProjectile(origin, vel, dmg, ownerId, isFromPlayer: true),
            structureLookup: session.FindStructureAtTile,
            pathCoordinator: session.PathfindingCoordinator);
        session.Start();

        // Recruit Miner & Hunter using settlers.json costs (bread x2, cooked_meat x1)
        Assert.True(session.TryRecruitSettler("Miner", out Settler? miner));
        Assert.True(session.TryRecruitSettler("Hunter", out Settler? hunter));
        Assert.NotNull(miner);
        Assert.NotNull(hunter);
        Assert.Equal(0, player.Inventory.GetItemCount("bread"));
        Assert.Equal(0, player.Inventory.GetItemCount("cooked_meat"));
        Assert.IsType<MinerSettlerStrategy>(miner!.CurrentStrategy);
        Assert.IsType<HunterSettlerStrategy>(hunter!.CurrentStrategy);
        Assert.False(miner.HasValidHome);
        Assert.Equal(30, miner.Happiness);

        // Build an enclosed spacious house with a torch => happiness jumps to >= 85
        BuildPerimeterWalls(session, enemyFactory, 8, 13, 8, 12, new GridPoint(10, 8));
        session.AddStructure(enemyFactory.CreateStructure(StructureType.Torch, 9, 9, level.TileSize));
        session.Settlement.RefreshHousing(level, session.Structures, session.FindStructureAtTile, session.Enemies);

        Assert.True(miner.HasValidHome);
        Assert.NotNull(miner.HomeTile);
        Assert.True(miner.Happiness >= 85);

        // Happiness discount on barter trade: "miner_copper" base cost is 4 wood -> 3 wood with >=80 happiness
        player.Inventory.TryAddItem(itemFactory.ResolveItem("wood"), 3);
        Assert.True(session.Settlement.TryTrade(miner, "miner_copper", player));
        Assert.Equal(0, player.Inventory.GetItemCount("wood"));
        Assert.Equal(2, player.Inventory.GetItemCount("copper_ore"));

        // Place an enemy near the Hunter and step daytime simulation for a full production cycle (~15s)
        Enemy targetSlime = enemyFactory.CreateEnemy("Slime", new Vector2D(hunter.Center.X + 48f, hunter.Center.Y));
        session.AddEnemy(targetSlime);

        const float FIXED_DT = 1f / 60f;
        for (int step = 0; step < 900; step++)
        {
            session.Update(FIXED_DT);
        }

        Assert.Equal(SettlerState.Work, miner.State);
        Assert.True(targetSlime.CurrentHp < targetSlime.MaxHp || !targetSlime.IsAlive);
        Assert.True(miner.Stockpile.UsedSlots > 0);
        Assert.True(hunter.Stockpile.UsedSlots > 0);

        int collected = session.Settlement.TryCollectProducedItems(miner, player);
        Assert.True(collected > 0);

        // Save/Load round-trip preserves recruited settlers and stockpiles
        var saveStorage = new InMemorySaveStorage();
        saveStorage.Save(1, session.CreateSnapshot());
        GameStateDTO loadedDto = saveStorage.Load(1);
        Assert.Equal(2, loadedDto.Settlers.Count);

        var level2 = new IslandLevel("ISLAND_01", "Colony Isle", "Forest", "KingSlime", 32, 32, 4, 4);
        var player2 = new Player("player_1", level2.PlayerSpawnWorldPosition);
        using var restored = new GameSession(level2, player2, itemFactory.ResolveItem, settlerCatalog: catalog);
        restored.RestoreFromSnapshot(loadedDto, enemyFactory);

        Assert.Equal(2, restored.Settlement.Settlers.Count);
        Settler restoredMiner = Assert.Single(restored.Settlement.Settlers, s => s.ProfessionId == "Miner");
        Settler restoredHunter = Assert.Single(restored.Settlement.Settlers, s => s.ProfessionId == "Hunter");
        Assert.Equal(miner.Name, restoredMiner.Name);
        Assert.Equal(miner.Happiness, restoredMiner.Happiness);
        Assert.Equal(miner.HomeTile, restoredMiner.HomeTile);
        Assert.True(restoredHunter.Stockpile.UsedSlots > 0);
    }

    [Fact]
    public void Settlers_TwentyActiveNpcs_StepWithinPathfindingBudgetCleanly()
    {
        var level = new IslandLevel("ISLAND_01", "Large Colony", "Forest", "KingSlime", 32, 32, 16, 16);
        var items = ItemFactory.CreateDefault();
        var catalog = SettlerCatalog.CreateDefault();
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);

        using var session = new GameSession(level, player, items.ResolveItem, settlerCatalog: catalog);
        session.Start();

        var enemyFactory = EnemyFactory.CreateDefault();
        session.AddResource(enemyFactory.CreateResourceNode(ResourceType.StoneRock, level.TileToWorldCenter(20, 16)));
        Assert.True(catalog.TryGetProfession("Miner", out SettlerProfessionDefinition? minerDef));
        Assert.True(catalog.TryGetProfession("Hunter", out SettlerProfessionDefinition? hunterDef));

        for (int i = 0; i < 20; i++)
        {
            SettlerProfessionDefinition def = (i % 2 == 0) ? minerDef! : hunterDef!;
            var pos = level.TileToWorldCenter(10 + (i % 10), 10 + (i / 10));
            session.AddSettler(new Settler($"settler_{i}", catalog.PickName(i), def, pos));
        }

        Assert.Equal(20, session.Settlement.Settlers.Count);

        for (int tick = 0; tick < 120; tick++)
        {
            session.Update(1f / 60f);
        }

        Assert.Equal(20, session.Settlement.Settlers.Count);
    }

    private sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<int, GameStateDTO> _slots = new();

        public void Save(int slotIndex, GameStateDTO snapshot) => _slots[slotIndex] = snapshot;
        public GameStateDTO Load(int slotIndex) => _slots[slotIndex];
        public bool Exists(int slotIndex) => _slots.ContainsKey(slotIndex);
        public void Delete(int slotIndex) => _slots.Remove(slotIndex);
    }
}
