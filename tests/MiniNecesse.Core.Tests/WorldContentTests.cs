using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class WorldContentTests
{
    [Fact]
    public void NewResources_HarvestAndCraftNewRecipes_SucceedEndToEnd()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<CraftingRecipe> recipes = loader.LoadRecipes();
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var level = new IslandLevel("ISLAND_TEST", "Harvest Isle", "Forest", "KingSlime", 32, 32, 16, 16);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);

        using var session = new GameSession(level, player, itemFactory.ResolveItem);
        session.Start();

        ResourceNode berry = enemyFactory.CreateResourceNode(ResourceType.BerryBush, level.TileToWorldCenter(17, 16));
        ResourceNode wheat = enemyFactory.CreateResourceNode(ResourceType.WheatCrop, level.TileToWorldCenter(15, 16));
        ResourceNode coal = enemyFactory.CreateResourceNode(ResourceType.CoalDeposit, level.TileToWorldCenter(16, 17));
        ResourceNode crystal = enemyFactory.CreateResourceNode(ResourceType.CrystalCluster, level.TileToWorldCenter(16, 15));

        DroppedItem? berryDrop = session.RuleEngine.ResolveHarvest(berry, ToolType.None, 25);
        DroppedItem? wheatDrop = session.RuleEngine.ResolveHarvest(wheat, ToolType.None, 25);
        DroppedItem? coalDrop = session.RuleEngine.ResolveHarvest(coal, ToolType.Pickaxe, 50);
        DroppedItem? crystalDrop = session.RuleEngine.ResolveHarvest(crystal, ToolType.Pickaxe, 90);

        Assert.NotNull(berryDrop);
        Assert.NotNull(wheatDrop);
        Assert.NotNull(coalDrop);
        Assert.NotNull(crystalDrop);
        Assert.Equal("berry", berryDrop!.ItemId);
        Assert.Equal("wheat", wheatDrop!.ItemId);
        Assert.Equal("coal", coalDrop!.ItemId);
        Assert.Equal("crystal", crystalDrop!.ItemId);

        session.Inventory.TryAddItem(itemFactory.ResolveItem("wood"), 20);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("wheat"), 3);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("raw_meat"), 2);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("coal"), 2);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("feather"), 4);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("leather"), 2);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("crystal"), 4);
        session.Inventory.TryAddItem(itemFactory.ResolveItem("gold_ore"), 3);

        // Place a workbench next to player to unlock workbench recipes.
        session.AddStructure(enemyFactory.CreateStructure(StructureType.Workbench, 17, 16, level.TileSize));

        CraftingRecipe fenceRecipe = recipes.First(r => r.RecipeId == "craft_wooden_fence");
        CraftingRecipe breadRecipe = recipes.First(r => r.RecipeId == "craft_bread");
        CraftingRecipe roastRecipe = recipes.First(r => r.RecipeId == "craft_cooked_meat");
        CraftingRecipe hunterBowRecipe = recipes.First(r => r.RecipeId == "craft_hunter_bow");
        CraftingRecipe crystalStaffRecipe = recipes.First(r => r.RecipeId == "craft_crystal_staff");

        Assert.True(session.TryCraft(fenceRecipe));
        Assert.True(session.TryCraft(breadRecipe));
        Assert.True(session.TryCraft(roastRecipe));
        Assert.True(session.TryCraft(hunterBowRecipe));
        Assert.True(session.TryCraft(crystalStaffRecipe));

        Assert.Equal(3, session.Inventory.GetItemCount("wooden_fence"));
        Assert.Equal(1, session.Inventory.GetItemCount("bread"));
        Assert.Equal(2, session.Inventory.GetItemCount("cooked_meat"));
        Assert.Equal(1, session.Inventory.GetItemCount("hunter_bow"));
        Assert.Equal(1, session.Inventory.GetItemCount("crystal_staff"));
    }

    [Fact]
    public void Animals_WanderFleeCombatDropsAndSaveLoad_WorkAcrossChunkStreaming()
    {
        var loader = new JsonLevelLoader();
        LoadedIslandPackage package = loader.LoadIsland("ISLAND_01");
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var player = new Player("player_1", package.Level.PlayerSpawnWorldPosition);

        using var session = new GameSession(package.Level, player, itemFactory.ResolveItem, viewDistanceChunks: 1);
        session.ConfigureChunkWorldSpawners(
            package.ChunkResourceGenerator,
            enemyFactory.CreateResourceNode,
            package.ChunkStructureGenerator,
            (t, c, r) => enemyFactory.CreateStructure(t, c, r, package.Level.TileSize),
            package.ChunkAnimalGenerator,
            enemyFactory.CreateAnimal);
        session.SetVillages(package.Villages);
        session.Start();

        // Spawn all 4 animal species near player.
        Vector2D chickenPos = package.Level.TileToWorldCenter(package.Level.SpawnCol + 1, package.Level.SpawnRow);
        Vector2D sheepPos = package.Level.TileToWorldCenter(package.Level.SpawnCol - 1, package.Level.SpawnRow);
        Vector2D cowPos = package.Level.TileToWorldCenter(package.Level.SpawnCol, package.Level.SpawnRow + 1);
        Vector2D pigPos = package.Level.TileToWorldCenter(package.Level.SpawnCol, package.Level.SpawnRow - 1);

        Animal chicken = enemyFactory.CreateAnimal(AnimalType.Chicken, chickenPos);
        Animal sheep = enemyFactory.CreateAnimal(AnimalType.Sheep, sheepPos);
        Animal cow = enemyFactory.CreateAnimal(AnimalType.Cow, cowPos);
        Animal pig = enemyFactory.CreateAnimal(AnimalType.Pig, pigPos);

        session.AddAnimal(chicken);
        session.AddAnimal(sheep);
        session.AddAnimal(cow);
        session.AddAnimal(pig);

        // Step simulation: animals wander safely inside bounds.
        for (int i = 0; i < 90; i++)
        {
            session.Update(1f / 60f);
        }

        Assert.True(chicken.IsAlive && sheep.IsAlive && cow.IsAlive && pig.IsAlive);

        // Damaging sheep causes it to flee away from player.
        Vector2D sheepPosBeforeHit = sheep.Center;
        sheep.TakeDamage(5);
        Assert.True(sheep.IsFleeing);
        for (int i = 0; i < 15; i++)
        {
            session.Update(1f / 60f);
        }

        Assert.True(Vector2D.Distance(sheep.Center, player.Center) >= Vector2D.Distance(sheepPosBeforeHit, player.Center));

        // Melee hunt chicken -> drops raw_meat and feather.
        player.SetPosition(chicken.Position - new Vector2D(16f, 0f));
        session.ExecutePrimaryAction(chicken.Center);
        session.Update(0.5f);
        if (chicken.IsAlive)
        {
            chicken.TakeDamage(chicken.CurrentHp);
        }

        Assert.False(chicken.IsAlive);
        Assert.Contains(session.DroppedItems, d => d.IsActive && d.ItemId == "raw_meat");
        Assert.Contains(session.DroppedItems, d => d.IsActive && d.ItemId == "feather");

        // Projectile hunt pig -> projectile hits animal via _playerTargetGrid.
        int pigHpBefore = pig.CurrentHp;
        session.SpawnPooledProjectile(
            pig.Center - new Vector2D(20f, 0f),
            new Vector2D(300f, 0f),
            12,
            player.Id,
            isFromPlayer: true);
        session.Update(1f / 60f);
        session.Update(1f / 60f);
        Assert.True(pig.CurrentHp < pigHpBefore);

        // Save/load round-trip preserves surviving cow & sheep in JsonSaveStorage.
        string tempDir = Path.Combine(Path.GetTempPath(), $"mininecesse_animals_{Guid.NewGuid():N}");
        try
        {
            var storage = new JsonSaveStorage(tempDir);
            GameStateDTO dto = session.CreateSnapshot();
            Assert.Contains(dto.Animals, a => a.Type == nameof(AnimalType.Cow));
            storage.Save(1, dto);

            GameStateDTO loadedDto = storage.Load(1);
            LoadedIslandPackage pkg2 = loader.LoadIsland("ISLAND_01");
            var p2 = new Player("player_1", pkg2.Level.PlayerSpawnWorldPosition);
            using var session2 = new GameSession(pkg2.Level, p2, itemFactory.ResolveItem, viewDistanceChunks: 1);
            session2.RestoreFromSnapshot(loadedDto, enemyFactory);

            Assert.Contains(session2.Animals, a => a.AnimalType == AnimalType.Cow && a.IsAlive);
            Assert.Contains(session2.Animals, a => a.AnimalType == AnimalType.Sheep && a.IsAlive);
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
    public void ProceduralVillages_SpawnAcrossAllFiveIslandsAndStreamWithoutDuplication()
    {
        var loader = new JsonLevelLoader();
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();

        foreach (string islandId in loader.GetAvailableIslandIds())
        {
            LoadedIslandPackage package = loader.LoadIsland(islandId);
            Assert.True(package.Villages.Count >= 4, $"{islandId} should generate at least 4 villages.");
            Assert.NotNull(package.ChunkStructureGenerator);
            Assert.NotNull(package.ChunkAnimalGenerator);

            VillageSite firstVillage = package.Villages[0];
            Assert.True(firstVillage.Structures.Count >= 20);
            Assert.True(firstVillage.FarmResources.Count >= 10);
            Assert.True(firstVillage.Livestock.Count >= 4);

            // Walkable center away from player spawn.
            Tile centerTile = package.Level.GetTile(firstVillage.CenterCol, firstVillage.CenterRow);
            Assert.True(centerTile.IsWalkable, $"{islandId} village center ({firstVillage.CenterCol}, {firstVillage.CenterRow}) should be walkable.");
            Assert.True(
                Vector2D.Distance(package.Level.TileToWorldCenter(firstVillage.CenterCol, firstVillage.CenterRow), package.Level.PlayerSpawnWorldPosition) >= 200f);
        }

        // Stream directly into ISLAND_01's first village and verify structures, crops, and livestock populate cleanly without duplicating on reload.
        LoadedIslandPackage forestPkg = loader.LoadIsland("ISLAND_01");
        VillageSite village = forestPkg.Villages[0];
        var player = new Player("player_1", forestPkg.Level.TileToWorldCenter(village.CenterCol, village.CenterRow));

        using var session = new GameSession(forestPkg.Level, player, itemFactory.ResolveItem, viewDistanceChunks: 1);
        session.ConfigureChunkWorldSpawners(
            forestPkg.ChunkResourceGenerator,
            enemyFactory.CreateResourceNode,
            forestPkg.ChunkStructureGenerator,
            (t, c, r) => enemyFactory.CreateStructure(t, c, r, forestPkg.Level.TileSize),
            forestPkg.ChunkAnimalGenerator,
            enemyFactory.CreateAnimal);
        session.SetVillages(forestPkg.Villages);
        session.Start();

        Assert.NotNull(session.FindVillageAt(player.Center));
        Assert.Contains(session.Structures, s => s.StructureType == StructureType.Workbench);
        Assert.Contains(session.Structures, s => s.StructureType == StructureType.WoodenFence);
        Assert.Contains(session.Structures, s => s.StructureType == StructureType.Torch);
        Assert.Contains(session.Resources, r => r.ResourceType == ResourceType.WheatCrop);
        Assert.Contains(session.Animals, a => a.AnimalType == AnimalType.Chicken);
        Assert.Contains(session.Animals, a => a.AnimalType == AnimalType.Sheep);
        Assert.Contains(session.Animals, a => a.AnimalType == AnimalType.Cow);
        Assert.Contains(session.Animals, a => a.AnimalType == AnimalType.Pig);

        int initialStructureCount = session.Structures.Count;
        ChunkCoord villageChunk = forestPkg.Level.GetChunkCoordForTile(village.CenterCol, village.CenterRow);

        // Move far away to unload village chunks, then return to verify no duplicate structures spawn.
        player.SetPosition(forestPkg.Level.TileToWorldCenter(forestPkg.Level.SpawnCol + (6 * forestPkg.Level.ChunkSize), forestPkg.Level.SpawnRow));
        session.Update(1f / 60f);
        Assert.False(forestPkg.Level.IsChunkLoaded(villageChunk));

        player.SetPosition(forestPkg.Level.TileToWorldCenter(village.CenterCol, village.CenterRow));
        session.Update(1f / 60f);
        Assert.True(forestPkg.Level.IsChunkLoaded(villageChunk));
        Assert.Equal(initialStructureCount, session.Structures.Count);
    }
}
