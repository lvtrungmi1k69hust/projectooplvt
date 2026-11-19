using System;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Centralizes gameplay rules: scoring invariants (Score &gt;= 0), combat &amp; harvesting resolution,
/// collision separation, and Victory/GameOver outcome evaluation.
/// </summary>
public sealed class RuleEngine
{
    /// <summary>Score awarded upon depleting a natural resource node.</summary>
    public const int HARVEST_SCORE = 5;

    /// <summary>Score awarded upon crafting an item.</summary>
    public const int CRAFT_SCORE = 15;

    /// <summary>Default score awarded upon defeating a standard enemy.</summary>
    public const int ENEMY_KILL_SCORE = 50;

    /// <summary>Default score awarded upon defeating an island boss.</summary>
    public const int BOSS_KILL_SCORE = 500;

    /// <summary>Bonus score awarded upon clearing the current island.</summary>
    public const int ISLAND_CLEAR_BONUS = 300;

    private int _score;
    private int _dropSequence;

    /// <summary>Gets the current accumulated non-negative player score.</summary>
    public int Score => _score;

    /// <summary>Raised whenever the score changes.</summary>
    public event Action<int>? OnScoreChanged;

    /// <summary>Raised when an island boss is defeated.</summary>
    public event Action<Enemy>? OnBossDefeated;

    /// <summary>Raised when win conditions are met.</summary>
    public event Action<int>? OnVictoryTriggered;

    /// <summary>Raised when lose conditions are met.</summary>
    public event Action<int>? OnGameOverTriggered;

    /// <summary>
    /// Adds non-negative points to the score, strictly upholding the invariant Score &gt;= 0.
    /// </summary>
    public void AddScore(int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Score increment cannot be negative.");
        }

        if (points == 0)
        {
            return;
        }

        _score = checked(_score + points);
        OnScoreChanged?.Invoke(_score);
    }

    /// <summary>Restores a non-negative score from a saved session snapshot.</summary>
    public void RestoreScore(int score)
    {
        if (score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Restored score cannot be negative.");
        }

        _score = score;
        OnScoreChanged?.Invoke(_score);
    }

    /// <summary>Resets the score back to zero for a new run.</summary>
    public void ResetScore()
    {
        _score = 0;
        OnScoreChanged?.Invoke(_score);
    }

    /// <summary>Awards crafting points upon completing a recipe.</summary>
    public void RecordCraftCompleted() => AddScore(CRAFT_SCORE);

    /// <summary>
    /// Resolves a player's melee attack against any <see cref="IDamageable"/> target.
    /// </summary>
    public bool ResolveMeleeAttack(Player attacker, IDamageable target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        if (!attacker.CanAct || !target.IsAlive)
        {
            return false;
        }

        int damage = attacker.EquippedWeapon?.ComputeDamage() ?? attacker.HarvestPower;
        target.TakeDamage(damage);
        attacker.TriggerActionCooldown();
        return true;
    }

    /// <summary>Resolves a projectile hit against a living <see cref="IDamageable"/> target.</summary>
    public bool ResolveProjectileHit(Projectile projectile, IDamageable target)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        ArgumentNullException.ThrowIfNull(target);

        if (!projectile.IsActive || !target.IsAlive)
        {
            return false;
        }

        target.TakeDamage(projectile.Damage);
        projectile.Deactivate();
        return true;
    }

    /// <summary>
    /// Harvests a <see cref="ResourceNode"/> and returns a newly spawned <see cref="DroppedItem"/>
    /// if the node is depleted on this hit.
    /// </summary>
    public DroppedItem? ResolveHarvest(ResourceNode node, ToolType toolType, int toolPower)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!node.IsAlive)
        {
            return null;
        }

        node.Harvest(toolType, toolPower);
        if (node.IsAlive)
        {
            return null;
        }

        AddScore(HARVEST_SCORE);
        _dropSequence++;
        return new DroppedItem($"drop_{_dropSequence}", node.DropItemId, node.DropQuantity, node.Center);
    }

    /// <summary>
    /// Attempts to collect an <see cref="ICollectible"/> into <paramref name="inventory"/>.
    /// </summary>
    public bool ResolvePickup(ICollectible collectible, Inventory inventory, Func<string, Item> itemResolver)
    {
        ArgumentNullException.ThrowIfNull(collectible);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(itemResolver);

        return collectible.TryCollect((itemId, quantity) =>
        {
            Item resolvedItem = itemResolver(itemId);
            return inventory.TryAddItem(resolvedItem, quantity);
        });
    }

    /// <summary>
    /// Processes score rewards and boss defeat notifications polymorphically via <see cref="Enemy.IsBoss"/>.
    /// </summary>
    public void ResolveEnemyDefeated(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        AddScore(enemy.ScoreReward);

        if (enemy.IsBoss)
        {
            AddScore(ISLAND_CLEAR_BONUS);
            OnBossDefeated?.Invoke(enemy);
        }
    }

    /// <summary>
    /// Computes the minimum translation vector required to resolve an AABB overlap (UML Dependency on <see cref="CollisionInfo"/>).
    /// </summary>
    public Vector2D ResolveCollision(CollisionInfo info)
    {
        return info.SeparationNormal * info.PenetrationDepth;
    }

    /// <summary>
    /// Evaluates win/lose rules: returns <see cref="GameState.GameOver"/> when player HP &lt;= 0,
    /// <see cref="GameState.Victory"/> when the island boss is defeated, or <see cref="GameState.Playing"/> otherwise.
    /// </summary>
    public GameState EvaluateOutcome(Player player, bool bossDefeated)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!player.IsAlive)
        {
            OnGameOverTriggered?.Invoke(_score);
            return GameState.GameOver;
        }

        if (bossDefeated)
        {
            OnVictoryTriggered?.Invoke(_score);
            return GameState.Victory;
        }

        return GameState.Playing;
    }
}
