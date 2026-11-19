using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Decorator;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Patterns.Flyweight;
using MiniNecesse.Core.Patterns.Pool;

namespace MiniNecesse.Core.Tests;

public sealed class DesignPatternsTests
{
    [Fact]
    public void WeaponUpgradeDecorator_StackedFireAndSwift_EnhancesStatsWithoutModifyingBaseWeapon()
    {
        var baseSword = new WeaponItem("copper_sword", "Copper Sword", baseDamage: 20, attackRange: 46f, baseCooldown: 0.50f);

        IWeapon upgraded = new SwiftAttackDecorator(new FireDamageDecorator(baseSword, bonusDamage: 12), cooldownMultiplier: 0.80f);

        Assert.Equal(20, baseSword.ComputeDamage());
        Assert.Equal(0.50f, baseSword.ComputeCooldown(), precision: 4);
        Assert.Equal(32, upgraded.ComputeDamage());
        Assert.Equal(0.40f, upgraded.ComputeCooldown(), precision: 4);
        Assert.Contains("Blazing", upgraded.Name);
        Assert.Contains("Swift", upgraded.Name);
    }

    [Fact]
    public void SetAIStrategy_AtRuntime_SwapsEnemyBehaviorDynamically()
    {
        var level = new IslandLevel("ISLAND_AI", "AI Test", "Forest", "KingSlime", 16, 16, 8, 8);
        var player = new Player("player_1", level.TileToWorldCenter(8, 8));
        var meleeStrategy = new MeleeChaseStrategy();
        var rangedFired = false;
        var rangedStrategy = new RangedKiteStrategy((_, _, _, _) => rangedFired = true);

        var enemy = new Enemy("en_1", "Hybrid", level.TileToWorldCenter(5, 8), 80, 80f, 10, 150f, 0.5f, 50, meleeStrategy);
        Assert.Same(meleeStrategy, enemy.CurrentStrategy);

        enemy.SetAIStrategy(rangedStrategy);
        enemy.ExecuteAI(player, level, 0.016f);

        Assert.Same(rangedStrategy, enemy.CurrentStrategy);
        Assert.True(rangedFired);
    }

    [Fact]
    public void ObjectPool_ReleaseAndReacquire_ReusesSameProjectileInstanceWithoutNewAllocation()
    {
        int seq = 0;
        var pool = new ObjectPool<Projectile>(
            () => new Projectile($"p_{++seq}", Vector2D.Zero, Vector2D.UnitX, 10, 1f, "player_1", true),
            onRelease: p => p.Deactivate());

        Projectile first = pool.Acquire();
        pool.Release(first);
        Projectile reacquired = pool.Acquire();

        Assert.Same(first, reacquired);
        Assert.Equal(1, pool.TotalCreatedCount);
        Assert.Equal(1, pool.ActiveCount);
        Assert.Equal(0, pool.AvailableCount);
    }

    [Fact]
    public void Boss_BelowThirtyPercentHp_TransitionsToEnrageStrategyAndTilesShareFlyweight()
    {
        var level = new IslandLevel("ISLAND_BOSS", "Boss Test", "Forest", "KingSlime", 12, 12, 6, 6);
        var enemyFactory = EnemyFactory.CreateDefault();
        Boss boss = enemyFactory.CreateBoss("KingSlime", level.TileToWorldCenter(4, 4));
        bool enragedEventRaised = false;
        boss.OnEnraged += _ => enragedEventRaised = true;

        // Deal 75% HP damage so remaining HP = 25% (< 30% threshold).
        boss.TakeDamage((int)(boss.MaxHp * 0.75f));

        Assert.True(boss.IsEnraged);
        Assert.True(enragedEventRaised);
        Assert.Equal(EnemyState.Enraged, boss.State);
        Assert.Equal(nameof(BossEnrageStrategy), boss.CurrentStrategy?.StrategyName);

        // Verify GoF Flyweight: two grass tiles share the exact same TileDataFlyweight instance.
        Tile tileA = level.GetTile(3, 3);
        Tile tileB = level.GetTile(4, 4);
        Assert.Same(tileA.Flyweight, tileB.Flyweight);
    }
}
