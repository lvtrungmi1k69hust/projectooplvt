using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Physics;
using MiniNecesse.Core.Rules;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class OptimizationAndAuditFixesTests
{
    [Fact]
    public void TrySummonIslandBoss_AppliesJsonBossConfigurationStats()
    {
        var loader = new JsonLevelLoader();
        LoadedIslandPackage package = loader.LoadIsland("ISLAND_01");
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var player = new Player("player_1", package.Level.PlayerSpawnWorldPosition);

        using var session = new GameSession(package.Level, player, itemFactory.ResolveItem, difficulty: Difficulty.Normal);
        session.Start();

        bool summoned = session.TrySummonIslandBoss(package.Boss, enemyFactory);

        Assert.True(summoned);
        Assert.NotNull(session.ActiveBoss);
        Assert.Equal(package.Boss.MaxHp, session.ActiveBoss!.MaxHp);
        Assert.Equal(package.Boss.DisplayName, session.ActiveBoss.BossName);
        Assert.Equal(package.Boss.BossType, session.ActiveBoss.Archetype);
    }

    [Fact]
    public void RestoreFromSnapshot_DuringNight_DoesNotSpawnDuplicateWaveAndRestoresDifficulty()
    {
        var loader = new JsonLevelLoader();
        LoadedIslandPackage package1 = loader.LoadIsland("ISLAND_01");
        GameConfig config = loader.LoadConfig();
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var player1 = new Player("player_1", package1.Level.PlayerSpawnWorldPosition);
        var dayNight1 = new DayNightCycle(config.DayDurationSeconds, config.NightDurationSeconds);
        dayNight1.RestoreState(dayNumber: 2, elapsedInCycle: config.DayDurationSeconds + 10f);

        GameStateDTO snapshot;
        using (var session1 = new GameSession(package1.Level, player1, itemFactory.ResolveItem, dayNight1, difficulty: Difficulty.Hard))
        {
            snapshot = session1.CreateSnapshot();
        }

        Assert.Equal("Hard", snapshot.Difficulty);

        LoadedIslandPackage package2 = loader.LoadIsland("ISLAND_01");
        var player2 = new Player("player_1", package2.Level.PlayerSpawnWorldPosition);
        var dayNight2 = new DayNightCycle(config.DayDurationSeconds, config.NightDurationSeconds);
        using var session2 = new GameSession(package2.Level, player2, itemFactory.ResolveItem, dayNight2, difficulty: Difficulty.Easy);
        var spawner = new RaidWaveSpawner(package2.Level, Difficulty.Hard, package2.Waves, enemyFactory.CreateEnemy, seed: package2.Seed);
        session2.AttachWaveSpawner(spawner);

        session2.RestoreFromSnapshot(snapshot, enemyFactory);

        Assert.Equal(Difficulty.Hard, session2.Difficulty);
        Assert.Equal(DayPhase.Night, session2.DayNightCycle.CurrentPhase);
        Assert.Equal(2, session2.DayNightCycle.DayNumber);
        Assert.Empty(session2.Enemies);
    }

    [Fact]
    public void BossEnrageStrategy_RespectsMaxVolleyRangeAndAvoidsSimultaneousMeleeVolley()
    {
        var level = new IslandLevel("ISLAND_ENRAGE", "Enrage Test", "Forest", "KingSlime", 32, 32, 16, 16);
        int projectilesSpawned = 0;
        var strategy = new BossEnrageStrategy(
            projectileSpawner: (_, _, _, _) => projectilesSpawned++,
            volleyCount: 3,
            maxVolleyRange: 300f);

        var boss = new Enemy("boss_1", "KingSlime", new Vector2D(320f, 320f), 500, 80f, 20, attackRange: 48f, attackCooldown: 0.5f, scoreReward: 500, initialStrategy: strategy);
        var player = new Player("player_1", new Vector2D(336f, 320f), maxHp: 200);

        // 1. Inside melee range: deals melee hit only (no simultaneous projectile volley).
        boss.ExecuteAI(player, level, 0.016f);
        Assert.Equal(180, player.CurrentHp);
        Assert.Equal(0, projectilesSpawned);

        // 2. Beyond maxVolleyRange: charges toward player without wasting out-of-range volleys.
        boss.Update(0.6f);
        player.SetPosition(new Vector2D(800f, 320f));
        boss.ExecuteAI(player, level, 0.016f);
        Assert.Equal(0, projectilesSpawned);

        // 3. Inside medium ranged band (attackRange < d <= maxVolleyRange): fires volley.
        player.SetPosition(new Vector2D(500f, 320f));
        boss.ExecuteAI(player, level, 0.016f);
        Assert.Equal(3, projectilesSpawned);
    }

    [Fact]
    public void PathfindingCoordinator_CachesSharedPathsInvalidatesOnStructureAndRespectsTickBudget()
    {
        var level = new IslandLevel("ISLAND_COORD", "Coordinator Test", "Forest", "KingSlime", 16, 16, 2, 8);
        var coordinator = new PathfindingCoordinator(maxNodeIterations: 250, maxRepathsPerTick: 2, cacheTtlSeconds: 0.75f);
        var bufA = new List<GridPoint>(32);
        var bufB = new List<GridPoint>(32);
        var bufDeferred = new List<GridPoint> { new(1, 1) };

        coordinator.BeginTick(0.016f);
        Assert.True(coordinator.TryRequestPath(level, new GridPoint(2, 8), new GridPoint(8, 8), bufA, out _, out bool cached1));
        Assert.False(cached1);
        Assert.Equal(1, coordinator.TotalSearches);

        // Same query in same tick hits shared cache without spending tick budget.
        Assert.True(coordinator.TryRequestPath(level, new GridPoint(2, 8), new GridPoint(8, 8), bufB, out _, out bool cached2));
        Assert.True(cached2);
        Assert.Equal(bufA, bufB);
        Assert.Equal(1, coordinator.TotalSearches);

        // Second distinct query uses remaining budget slot (2/2).
        Assert.True(coordinator.TryRequestPath(level, new GridPoint(3, 8), new GridPoint(8, 8), bufB, out _, out bool cached3));
        Assert.False(cached3);
        Assert.Equal(2, coordinator.TotalSearches);

        // Third distinct query in the same tick is deferred while preserving caller buffer.
        Assert.False(coordinator.TryRequestPath(level, new GridPoint(4, 8), new GridPoint(8, 8), bufDeferred, out _, out _));
        Assert.Equal(1, coordinator.DeferredRequests);
        Assert.Single(bufDeferred);

        // Mutating structure occupancy bumps StructureVersion and invalidates cached route.
        level.SetStructureOccupancy(5, 8, true);
        coordinator.BeginTick(0.016f);
        Assert.True(coordinator.TryRequestPath(level, new GridPoint(2, 8), new GridPoint(8, 8), bufA, out _, out bool cachedAfterWall));
        Assert.False(cachedAfterWall);
        Assert.DoesNotContain(new GridPoint(5, 8), bufA);
    }

    [Fact]
    public void EntityInterpolation_LerpsPositionAndSnapsOnPooledReset()
    {
        var enemy = new Enemy("e1", "Slime", new Vector2D(100f, 100f), 40, 60f, 5, 24f, 1f);
        enemy.CapturePreviousPosition();
        enemy.SetPosition(new Vector2D(120f, 140f));

        Assert.Equal(new Vector2D(100f, 100f), enemy.GetInterpolatedPosition(0f));
        Assert.Equal(new Vector2D(110f, 120f), enemy.GetInterpolatedPosition(0.5f));
        Assert.Equal(new Vector2D(120f, 140f), enemy.GetInterpolatedPosition(1f));

        var proj = new Projectile("p1", new Vector2D(10f, 10f), new Vector2D(100f, 0f), 10, 2f, "player_1", true);
        proj.CapturePreviousPosition();
        proj.Update(0.5f);
        proj.Reset(new Vector2D(500f, 500f), new Vector2D(0f, 100f), 12, 2f, "player_1", true);

        Assert.Equal(new Vector2D(500f, 500f), proj.PreviousPosition);
        Assert.Equal(new Vector2D(500f, 500f), proj.GetInterpolatedPosition(0.25f));
    }

    [Fact]
    public void GameSession_CachedViewsAndDisposeAndClearReleasePoolsProperly()
    {
        var level = new IslandLevel("ISLAND_POOL", "Pool Test", "Forest", "KingSlime", 16, 16, 8, 8);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();

        using var session = new GameSession(level, player, itemFactory.ResolveItem);
        Assert.Same(session.Enemies, session.Enemies);
        Assert.Same(session.Structures, session.Structures);
        Assert.Same(session.Resources, session.Resources);
        Assert.Same(session.Animals, session.Animals);
        Assert.Same(session.Projectiles, session.Projectiles);
        Assert.Same(session.DroppedItems, session.DroppedItems);
        Assert.Same(session.Inventory.Stacks, session.Inventory.Stacks);
        Assert.Same(session.Level.LoadedChunkCoords, session.Level.LoadedChunkCoords);

        session.Start();
        session.SpawnPooledProjectile(player.Center, new Vector2D(100f, 0f), 10, player.Id, true);
        session.SpawnPooledDroppedItem("wood", 2, player.Center);
        int activeProjBeforeClear = session.ProjectilePool.ActiveCount;
        int activeDropBeforeClear = session.DroppedItemPool.ActiveCount;

        Assert.True(activeProjBeforeClear >= 1);
        Assert.True(activeDropBeforeClear >= 1);

        GameStateDTO dto = session.CreateSnapshot();
        session.RestoreFromSnapshot(dto, enemyFactory);

        Assert.Equal(0, session.ProjectilePool.ActiveCount);
        Assert.Equal(0, session.DroppedItemPool.ActiveCount);
    }

    [Fact]
    public void SpatialHashGrid_TrimsExcessBucketsAfterLongTravel()
    {
        var grid = new SpatialHashGrid<Entity>(cellSize: 64f);
        for (int i = 0; i < 650; i++)
        {
            var e = new DroppedItem($"d_{i}", "wood", 1, new Vector2D(i * 128f, 0f));
            grid.Insert(e);
        }

        Assert.True(grid.TotalBucketCount > 512);
        grid.Clear();

        // Only 1 active bucket on next tick (< 25% of >512 buckets) -> triggers trim on Clear.
        grid.Insert(new DroppedItem("d_single", "wood", 1, new Vector2D(0f, 0f)));
        grid.Clear();

        Assert.True(grid.TotalBucketCount < 50);
    }
}
