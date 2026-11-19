using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Tests.Mocks;

namespace MiniNecesse.Core.Tests;

public sealed class CombatAndInvariantsTests
{
    [Fact]
    public void TakeDamage_ExceedingCurrentHp_ClampsHpAtZero()
    {
        var player = new Player("player_1", new Vector2D(64f, 64f), maxHp: 100);

        player.TakeDamage(250);

        Assert.Equal(0, player.CurrentHp);
        Assert.False(player.IsAlive);
        Assert.Throws<ArgumentOutOfRangeException>(() => player.TakeDamage(-10));
    }

    [Fact]
    public void Heal_ExceedingMaxHp_ClampsHpAtMaxHp()
    {
        var player = new Player("player_1", new Vector2D(64f, 64f), maxHp: 100);
        player.TakeDamage(30);

        player.Heal(500);

        Assert.Equal(100, player.CurrentHp);
        Assert.Throws<ArgumentOutOfRangeException>(() => player.Heal(-5));
    }

    [Fact]
    public void ResolveEnemyDefeated_StandardEnemy_AddsExactScoreReward()
    {
        var ruleEngine = new RuleEngine();
        var enemy = new Enemy(
            "slime_1",
            "Slime",
            new Vector2D(128f, 128f),
            maxHp: 35,
            moveSpeed: 65f,
            attackDamage: 8,
            attackRange: 28f,
            attackCooldown: 1.0f,
            scoreReward: 50);

        ruleEngine.ResolveEnemyDefeated(enemy);

        Assert.Equal(50, ruleEngine.Score);
        Assert.Throws<ArgumentOutOfRangeException>(() => ruleEngine.AddScore(-1));
    }

    [Fact]
    public void Update_WhenPlayerHpDropsToZero_TransitionsToGameOverState()
    {
        var level = new IslandLevel("ISLAND_TEST", "Test Isle", "Forest", "KingSlime", 12, 12, 6, 6);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition, maxHp: 50);
        var itemFactory = ItemFactory.CreateDefault();
        var hudSpy = new SpyHudObserver();

        using var session = new GameSession(level, player, itemFactory.ResolveItem);
        session.AttachHudObserver(hudSpy);
        session.Start();

        player.TakeDamage(50);
        session.Update(GameLoopController.FIXED_DELTA_TIME);

        Assert.Equal(GameState.GameOver, session.CurrentState);
        Assert.Equal(0, hudSpy.LastHp);
        Assert.Contains(GameState.GameOver, hudSpy.ObservedStates);
    }

    [Fact]
    public void Update_WhenIslandBossDefeated_TransitionsToVictoryStateAndAwardsClearBonus()
    {
        var level = new IslandLevel("ISLAND_TEST", "Test Isle", "Forest", "KingSlime", 12, 12, 6, 6);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition, maxHp: 100);
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();
        var hudSpy = new SpyHudObserver();

        using var session = new GameSession(level, player, itemFactory.ResolveItem);
        session.AttachHudObserver(hudSpy);
        session.Start();

        Boss boss = enemyFactory.CreateBoss("KingSlime", new Vector2D(160f, 160f));
        session.SpawnBoss(boss);
        boss.TakeDamage(boss.MaxHp);
        session.Update(GameLoopController.FIXED_DELTA_TIME);

        Assert.Equal(GameState.Victory, session.CurrentState);
        Assert.Equal(boss.ScoreReward + RuleEngine.ISLAND_CLEAR_BONUS, session.RuleEngine.Score);
        Assert.False(hudSpy.BossBarVisible);
    }
}
