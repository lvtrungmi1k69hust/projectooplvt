using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class EndlessWorldGenerationTests
{
    [Fact]
    public void EndlessGenerator_SameSeed_IsOrderIndependentAndDeterministic()
    {
        var loader = new JsonLevelLoader();
        EndlessBiomeCatalog catalog = loader.LoadEndlessBiomeCatalog();
        Assert.Equal(5, catalog.Biomes.Count);

        ILevelGenerator generator = new SeededEndlessLevelGenerator(catalog);
        var options = new EndlessWorldOptions(Width: 128, Height: 128, BiomeCatalog: catalog);

        LoadedIslandPackage worldA = generator.GenerateWorld(seed: 424242, options);
        LoadedIslandPackage worldB = generator.GenerateWorld(seed: 424242, options);

        // Load chunks on worldA in forward order and on worldB in reversed order
        var coords = new List<ChunkCoord>();
        for (int cy = 0; cy < worldA.Level.ChunkRows; cy++)
        {
            for (int cx = 0; cx < worldA.Level.ChunkColumns; cx++)
            {
                coords.Add(new ChunkCoord(cx, cy));
            }
        }

        for (int i = 0; i < coords.Count; i++)
        {
            worldA.Level.LoadChunk(coords[i]);
        }

        for (int i = coords.Count - 1; i >= 0; i--)
        {
            worldB.Level.LoadChunk(coords[i]);
        }

        // Verify tiles and resolved biomes match 100%
        var typedGen = (SeededEndlessLevelGenerator)generator;
        for (int r = 0; r < worldA.Level.Height; r += 3)
        {
            for (int c = 0; c < worldA.Level.Width; c += 3)
            {
                Assert.Equal(worldA.Level.GetTile(c, r).TileType, worldB.Level.GetTile(c, r).TileType);
                Assert.Equal(
                    typedGen.GetBiomeAtTile(c, r, 424242, options).BiomeId,
                    typedGen.GetBiomeAtTile(c, r, 424242, options).BiomeId);
            }
        }

        // Verify chunk resources, structures, and animals match identically
        Assert.NotNull(worldA.ChunkResourceGenerator);
        Assert.NotNull(worldA.ChunkStructureGenerator);
        Assert.NotNull(worldA.ChunkAnimalGenerator);

        var resA = new List<ResourceSpawnPoint>();
        var resB = new List<ResourceSpawnPoint>();
        var strA = new List<StructureSpawnPoint>();
        var strB = new List<StructureSpawnPoint>();
        var aniA = new List<AnimalSpawnPoint>();
        var aniB = new List<AnimalSpawnPoint>();

        foreach (ChunkCoord c in coords)
        {
            worldA.ChunkResourceGenerator!(c, resA);
            worldB.ChunkResourceGenerator!(c, resB);
            Assert.Equal(resA, resB);

            worldA.ChunkStructureGenerator!(c, strA);
            worldB.ChunkStructureGenerator!(c, strB);
            Assert.Equal(strA, strB);

            worldA.ChunkAnimalGenerator!(c, aniA);
            worldB.ChunkAnimalGenerator!(c, aniB);
            Assert.Equal(aniA, aniB);
        }
    }

    [Fact]
    public void EndlessGenerator_DifferentSeeds_ProduceDistinctMultiBiomeWorldsWithWalkableSpawn()
    {
        var catalog = EndlessBiomeCatalog.CreateDefault();
        var generator = new SeededEndlessLevelGenerator(catalog);
        var options = new EndlessWorldOptions(Width: 384, Height: 384, BiomeCatalog: catalog);

        LoadedIslandPackage world1 = generator.GenerateWorld(seed: 1001, options);
        LoadedIslandPackage world2 = generator.GenerateWorld(seed: 99999, options);

        // Guaranteed walkable starter clearing around spawn
        for (int dr = -2; dr <= 2; dr++)
        {
            for (int dc = -2; dc <= 2; dc++)
            {
                Assert.True(world1.Level.GetTile(world1.Level.SpawnCol + dc, world1.Level.SpawnRow + dr).IsWalkable);
            }
        }

        // Sample across the 384x384 world for biome diversity and seed difference
        var biomesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int differingBiomes = 0;
        for (int r = 16; r < 368; r += 16)
        {
            for (int c = 16; c < 368; c += 16)
            {
                string b1 = generator.GetBiomeAtTile(c, r, 1001, options).BiomeId;
                string b2 = generator.GetBiomeAtTile(c, r, 99999, options).BiomeId;
                biomesSeen.Add(b1);
                if (!string.Equals(b1, b2, StringComparison.OrdinalIgnoreCase))
                {
                    differingBiomes++;
                }
            }
        }

        Assert.True(biomesSeen.Count >= 3);
        Assert.True(differingBiomes > 20);
        Assert.True(world1.Waves.Count >= 10);
    }

    [Fact]
    public void EndlessWorld_ChunkStreamingAndSaveLoadRoundTrip_PreserveSeedAndRestoreIdenticalWorld()
    {
        var generator = new SeededEndlessLevelGenerator(EndlessBiomeCatalog.CreateDefault());
        var itemFactory = ItemFactory.CreateDefault();
        const int TEST_SEED = 778899;

        LoadedIslandPackage pkg1 = generator.GenerateWorld(TEST_SEED);
        Assert.Equal(384, pkg1.Level.Width);
        Assert.Equal(384, pkg1.Level.Height);
        Assert.Equal(576, pkg1.Level.TotalChunkCount);

        var player1 = new Player("player_1", pkg1.Level.PlayerSpawnWorldPosition);
        using var session1 = new GameSession(
            pkg1.Level,
            player1,
            itemFactory.ResolveItem,
            viewDistanceChunks: 2,
            minViewDistanceChunks: 1,
            maxViewDistanceChunks: 8,
            gameMode: GameMode.Endless,
            worldSeed: TEST_SEED);
        var enemyFactory1 = EnemyFactory.CreateDefault();
        session1.ConfigureChunkWorldSpawners(
            pkg1.ChunkResourceGenerator,
            enemyFactory1.CreateResourceNode,
            pkg1.ChunkStructureGenerator,
            (t, c, r) => enemyFactory1.CreateStructure(t, c, r, pkg1.Level.TileSize),
            pkg1.ChunkAnimalGenerator,
            enemyFactory1.CreateAnimal);
        session1.Start();
        session1.InitializeChunksAroundPlayer();

        // Only (2*2+1)^2 = 25 out of 576 chunks loaded!
        Assert.Equal(25, pkg1.Level.LoadedChunkCount);

        // Move far east and stream chunks over a few ticks (respecting per-tick chunk load budget):
        // old spawn chunk unloads and active loaded count matches view window (25 out of 576).
        ChunkCoord oldSpawnChunk = ChunkCoord.FromTile(pkg1.Level.SpawnCol, pkg1.Level.SpawnRow, pkg1.Level.ChunkSize);
        session1.Player.TeleportTo(pkg1.Level.TileToWorldCenter(pkg1.Level.SpawnCol + 96, pkg1.Level.SpawnRow));
        for (int tick = 0; tick < 8; tick++)
        {
            session1.Update(1f / 60f);
        }

        Assert.False(pkg1.Level.IsChunkLoaded(oldSpawnChunk));
        Assert.Equal(25, pkg1.Level.LoadedChunkCount);

        // Place a structure at the player's new region and snapshot
        int benchCol = pkg1.Level.SpawnCol + 98;
        int benchRow = pkg1.Level.SpawnRow;
        session1.AddStructure(enemyFactory1.CreateStructure(StructureType.Workbench, benchCol, benchRow, pkg1.Level.TileSize));

        var saveStorage = new LocalSaveStorage();
        saveStorage.Save(1, session1.CreateSnapshot());
        GameStateDTO loadedDto = saveStorage.Load(1);

        Assert.Equal("Endless", loadedDto.GameMode);
        Assert.Equal(TEST_SEED, loadedDto.WorldSeed);

        // Reconstruct world solely from persisted loadedDto.WorldSeed
        LoadedIslandPackage pkg2 = generator.GenerateWorld(loadedDto.WorldSeed);
        var player2 = new Player("player_1", pkg2.Level.PlayerSpawnWorldPosition);
        using var session2 = new GameSession(
            pkg2.Level,
            player2,
            itemFactory.ResolveItem,
            viewDistanceChunks: 2,
            minViewDistanceChunks: 1,
            maxViewDistanceChunks: 8,
            gameMode: GameMode.Endless,
            worldSeed: loadedDto.WorldSeed);
        var enemyFactory2 = EnemyFactory.CreateDefault();
        session2.ConfigureChunkWorldSpawners(
            pkg2.ChunkResourceGenerator,
            enemyFactory2.CreateResourceNode,
            pkg2.ChunkStructureGenerator,
            (t, c, r) => enemyFactory2.CreateStructure(t, c, r, pkg2.Level.TileSize),
            pkg2.ChunkAnimalGenerator,
            enemyFactory2.CreateAnimal);
        session2.RestoreFromSnapshot(loadedDto, enemyFactory2);

        Assert.Equal(GameMode.Endless, session2.GameMode);
        Assert.Equal(TEST_SEED, session2.WorldSeed);
        Assert.NotNull(session2.FindStructureAtTile(new MiniNecesse.Core.AI.GridPoint(benchCol, benchRow)));
        Assert.Equal(
            pkg1.Level.GetTile(benchCol, benchRow).TileType,
            pkg2.Level.GetTile(benchCol, benchRow).TileType);
    }

    private sealed class LocalSaveStorage : ISaveStorage
    {
        private readonly Dictionary<int, GameStateDTO> _slots = new();
        public void Save(int slotIndex, GameStateDTO snapshot) => _slots[slotIndex] = snapshot;
        public GameStateDTO Load(int slotIndex) => _slots[slotIndex];
        public bool Exists(int slotIndex) => _slots.ContainsKey(slotIndex);
        public void Delete(int slotIndex) => _slots.Remove(slotIndex);
    }
}
