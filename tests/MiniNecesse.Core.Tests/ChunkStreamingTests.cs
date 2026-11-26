using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class ChunkStreamingTests
{
    private static (IslandLevel Level, GameSession Session, EnemyFactory Factory, LoadedIslandPackage Package) CreateProceduralSession(
        int viewDistanceChunks = 2,
        string islandId = "ISLAND_01")
    {
        var loader = new JsonLevelLoader();
        LoadedIslandPackage package = loader.LoadIsland(islandId);
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var player = new Player("player_1", package.Level.PlayerSpawnWorldPosition);
        var session = new GameSession(
            package.Level,
            player,
            itemFactory.ResolveItem,
            viewDistanceChunks: viewDistanceChunks);

        session.ConfigureChunkResourceSpawner(package.ChunkResourceGenerator, enemyFactory.CreateResourceNode);
        return (package.Level, session, enemyFactory, package);
    }

    [Fact]
    public void LazyProceduralIsland_LoadsOnlySpawnChunkBeforeStartAndViewWindowAfterInit()
    {
        (IslandLevel level, GameSession session, _, _) = CreateProceduralSession(viewDistanceChunks: 2);
        using (session)
        {
            Assert.Equal(320, level.Width);
            Assert.Equal(320, level.Height);
            Assert.Equal(400, level.TotalChunkCount);
            Assert.True(level.UsesLazyChunkLoading);
            Assert.Equal(1, level.LoadedChunkCount);

            session.InitializeChunksAroundPlayer();

            // ViewDistance = 2 => (2*2 + 1)^2 = 25 chunks around center (10, 10)
            Assert.Equal(25, level.LoadedChunkCount);
            Assert.False(level.IsChunkLoaded(new ChunkCoord(0, 0)));
            Assert.True(level.IsChunkLoaded(new ChunkCoord(10, 10)));
        }
    }

    [Fact]
    public void PlayerMovingAcrossChunks_StreamsNewChunksAndUnloadsBeyondHysteresis()
    {
        (IslandLevel level, GameSession session, _, _) = CreateProceduralSession(viewDistanceChunks: 1);
        using (session)
        {
            session.Start();
            ChunkCoord startChunk = level.GetChunkCoordForWorld(session.Player.Center);
            Assert.Equal(9, level.LoadedChunkCount);
            Assert.True(level.IsChunkLoaded(startChunk));

            // Move player 5 chunks east (5 * 16 * 32 = 2560 px), well past ViewDistance(1) + Hysteresis(1) = 2.
            Vector2D farEastPos = level.TileToWorldCenter(level.SpawnCol + (5 * level.ChunkSize), level.SpawnRow);
            session.Player.SetPosition(farEastPos);
            session.Update(1f / 60f);

            ChunkCoord newCenter = level.GetChunkCoordForWorld(session.Player.Center);
            Assert.Equal(startChunk.ChunkX + 5, newCenter.ChunkX);
            Assert.True(level.IsChunkLoaded(newCenter));
            Assert.True(level.IsChunkLoaded(new ChunkCoord(newCenter.ChunkX + 1, newCenter.ChunkY)));
            Assert.False(level.IsChunkLoaded(startChunk));
        }
    }

    [Fact]
    public void SetViewDistanceChunks_AtRuntime_ImmediatelyExpandsAndShrinksLoadedChunks()
    {
        (IslandLevel level, GameSession session, _, _) = CreateProceduralSession(viewDistanceChunks: 1);
        using (session)
        {
            session.Start();
            Assert.Equal(9, level.LoadedChunkCount);

            session.SetViewDistanceChunks(3);
            Assert.Equal(3, session.ViewDistanceChunks);
            Assert.Equal(49, level.LoadedChunkCount);

            session.SetViewDistanceChunks(1);
            Assert.Equal(1, session.ViewDistanceChunks);
            Assert.Equal(9, level.LoadedChunkCount);
        }
    }

    [Fact]
    public void ChunkUnloadAndReload_PreservesHarvestedResourcesAndPlacedStructures()
    {
        (IslandLevel level, GameSession session, EnemyFactory enemyFactory, _) = CreateProceduralSession(viewDistanceChunks: 1);
        using (session)
        {
            session.Start();
            ChunkCoord spawnChunk = level.GetChunkCoordForWorld(session.Player.Center);

            // Pick a resource in spawnChunk and harvest it completely.
            ResourceNode? targetResource = null;
            for (int i = 0; i < session.Resources.Count; i++)
            {
                ResourceNode node = session.Resources[i];
                if (level.GetChunkCoordForWorld(node.Center) == spawnChunk)
                {
                    targetResource = node;
                    break;
                }
            }

            Assert.NotNull(targetResource);
            string harvestedId = targetResource!.Id;
            int countInSpawnChunkBefore = session.Resources.Count(r => level.GetChunkCoordForWorld(r.Center) == spawnChunk);
            targetResource.TakeDamage(targetResource.MaxHp);
            session.Update(1f / 60f);

            // Place a solid wooden wall in spawnChunk.
            int wallCol = level.SpawnCol + 1;
            int wallRow = level.SpawnRow;
            Structure wall = enemyFactory.CreateStructure(StructureType.WoodenWall, wallCol, wallRow, level.TileSize);
            session.AddStructure(wall);
            Assert.False(level.IsTileWalkable(wallCol, wallRow));
            Assert.NotNull(session.FindStructureAtTile(new GridPoint(wallCol, wallRow)));

            // Move 5 chunks east so spawnChunk unloads completely.
            session.Player.SetPosition(level.TileToWorldCenter(level.SpawnCol + (5 * level.ChunkSize), level.SpawnRow));
            session.Update(1f / 60f);
            Assert.False(level.IsChunkLoaded(spawnChunk));
            Assert.Null(session.FindStructureAtTile(new GridPoint(wallCol, wallRow)));

            // Return to spawn so spawnChunk reloads.
            session.Player.SetPosition(level.PlayerSpawnWorldPosition);
            session.Update(1f / 60f);
            Assert.True(level.IsChunkLoaded(spawnChunk));

            // Harvested resource must remain gone (not regenerated); structure must be restored and solid.
            Assert.DoesNotContain(session.Resources, r => r.Id == harvestedId);
            int countInSpawnChunkAfter = session.Resources.Count(r => level.GetChunkCoordForWorld(r.Center) == spawnChunk);
            Assert.Equal(countInSpawnChunkBefore - 1, countInSpawnChunkAfter);
            Assert.False(level.IsTileWalkable(wallCol, wallRow));
            Structure? restoredWall = session.FindStructureAtTile(new GridPoint(wallCol, wallRow));
            Assert.NotNull(restoredWall);
            Assert.Equal(StructureType.WoodenWall, restoredWall!.StructureType);
        }
    }

    [Fact]
    public void SaveAndLoad_PreservesHibernatedStructuresAndVisitedChunksWhenUnloaded()
    {
        var loader = new JsonLevelLoader();
        LoadedIslandPackage package1 = loader.LoadIsland("ISLAND_01");
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var player1 = new Player("player_1", package1.Level.PlayerSpawnWorldPosition);

        GameStateDTO savedDto;
        int wallCol = package1.Level.SpawnCol + 1;
        int wallRow = package1.Level.SpawnRow;
        ChunkCoord spawnChunk = package1.Level.GetChunkCoordForTile(wallCol, wallRow);

        using (var session1 = new GameSession(package1.Level, player1, itemFactory.ResolveItem, viewDistanceChunks: 1))
        {
            session1.ConfigureChunkResourceSpawner(package1.ChunkResourceGenerator, enemyFactory.CreateResourceNode);
            session1.Start();

            session1.AddStructure(enemyFactory.CreateStructure(StructureType.StoneWall, wallCol, wallRow, package1.Level.TileSize));

            // Move 5 chunks away so spawnChunk & its wall hibernate before saving.
            session1.Player.SetPosition(package1.Level.TileToWorldCenter(package1.Level.SpawnCol + (5 * package1.Level.ChunkSize), package1.Level.SpawnRow));
            session1.Update(1f / 60f);
            Assert.False(package1.Level.IsChunkLoaded(spawnChunk));

            savedDto = session1.CreateSnapshot();
        }

        Assert.Contains(savedDto.Structures, s => s.TileCol == wallCol && s.TileRow == wallRow);
        Assert.Contains(savedDto.GeneratedChunks, c => c.ChunkX == spawnChunk.ChunkX && c.ChunkY == spawnChunk.ChunkY);

        // Restore into a fresh session; player starts 5 chunks east so spawnChunk is initially unloaded.
        LoadedIslandPackage package2 = loader.LoadIsland("ISLAND_01");
        var player2 = new Player("player_1", package2.Level.PlayerSpawnWorldPosition);
        using var session2 = new GameSession(package2.Level, player2, itemFactory.ResolveItem, viewDistanceChunks: 1);
        session2.ConfigureChunkResourceSpawner(package2.ChunkResourceGenerator, enemyFactory.CreateResourceNode);
        session2.RestoreFromSnapshot(savedDto, enemyFactory);
        session2.Start();

        Assert.False(package2.Level.IsChunkLoaded(spawnChunk));

        // Walk back to spawn and confirm the saved StoneWall reappears intact and solid.
        session2.Player.SetPosition(package2.Level.PlayerSpawnWorldPosition);
        session2.Update(1f / 60f);

        Assert.True(package2.Level.IsChunkLoaded(spawnChunk));
        Assert.False(package2.Level.IsTileWalkable(wallCol, wallRow));
        Structure? restoredWall = session2.FindStructureAtTile(new GridPoint(wallCol, wallRow));
        Assert.NotNull(restoredWall);
        Assert.Equal(StructureType.StoneWall, restoredWall!.StructureType);
    }

    [Fact]
    public void RaidWaveSpawnerAndBossSummon_SpawnInsideLoadedChunksNearPlayer()
    {
        (IslandLevel level, GameSession session, EnemyFactory enemyFactory, LoadedIslandPackage package) = CreateProceduralSession(viewDistanceChunks: 2);
        using (session)
        {
            var spawner = new RaidWaveSpawner(level, Difficulty.Normal, package.Waves, enemyFactory.CreateEnemy, seed: package.Seed);
            session.AttachWaveSpawner(spawner);
            session.Start();

            var spawnedWave = new List<Enemy>();
            int count = spawner.SpawnNextWave(session.Player, spawnedWave);
            Assert.True(count > 0);
            Assert.All(spawnedWave, enemy =>
            {
                ChunkCoord coord = level.GetChunkCoordForWorld(enemy.Center);
                Assert.True(level.IsChunkLoaded(coord));
                Assert.True(Vector2D.Distance(enemy.Center, session.Player.Center) >= 160f);
            });

            bool bossSummoned = session.TrySummonIslandBoss(package.Boss, enemyFactory);
            Assert.True(bossSummoned);
            Assert.NotNull(session.ActiveBoss);
            ChunkCoord bossChunk = level.GetChunkCoordForWorld(session.ActiveBoss!.Center);
            Assert.True(level.IsChunkLoaded(bossChunk));
            Assert.InRange(Vector2D.Distance(session.ActiveBoss.Center, session.Player.Center), 160f, 640f);
        }
    }
}
