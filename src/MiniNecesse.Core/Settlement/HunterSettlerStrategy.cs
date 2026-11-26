using System;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Settler strategy for Hunters: patrols colony perimeters, fires arrows at nearby threats,
/// and gathers meat/leather/feathers into stockpile.
/// </summary>
public sealed class HunterSettlerStrategy : SettlerStrategyBase
{
    private const float GUARD_DETECT_RADIUS_SQ = 240f * 240f;
    private const float ATTACK_RANGE_SQ = 160f * 160f;
    private const int HUNTER_DAMAGE = 14;
    private const float ARROW_SPEED = 310f;

    /// <inheritdoc />
    public override string StrategyName => nameof(HunterSettlerStrategy);

    /// <inheritdoc />
    public override SettlerProfession Profession => SettlerProfession.Hunter;

    /// <summary>Initializes a new instance of the <see cref="HunterSettlerStrategy"/> class.</summary>
    public HunterSettlerStrategy(
        Func<string, Item> itemResolver,
        PathfindingCoordinator pathCoordinator,
        CollisionDetector collisionDetector,
        float initialStaggerSeconds = 0f)
        : base(itemResolver, pathCoordinator, collisionDetector, initialStaggerSeconds)
    {
    }

    /// <inheritdoc />
    protected override void ExecuteWork(Settler settler, Player player, IslandLevel level, float deltaTime)
    {
        Enemy? threat = FindNearestEnemyInGuardRange(settler.Center);
        if (threat is not null)
        {
            float distSq = Vector2D.DistanceSquared(settler.Center, threat.Center);
            if (distSq <= ATTACK_RANGE_SQ)
            {
                if (settler.CanAttack)
                {
                    if (ContextProjectileSpawner is not null)
                    {
                        Vector2D dir = (threat.Center - settler.Center).Normalized();
                        if (dir != Vector2D.Zero)
                        {
                            ContextProjectileSpawner(settler.Center, dir * ARROW_SPEED, HUNTER_DAMAGE, settler.Id);
                        }
                    }
                    else
                    {
                        threat.TakeDamage(HUNTER_DAMAGE);
                    }

                    settler.TriggerAttackCooldown(1.1f);
                }
            }
            else
            {
                NavigateToward(settler, threat.Center, level, deltaTime);
            }
        }
        else if (settler.HomeTile.HasValue)
        {
            Vector2D homeWorld = level.TileToWorldCenter(settler.HomeTile.Value.Col, settler.HomeTile.Value.Row);
            if (Vector2D.DistanceSquared(settler.Center, homeWorld) > 84f * 84f)
            {
                NavigateToward(settler, homeWorld, level, deltaTime);
            }
        }

        settler.TryAdvanceProduction(deltaTime, ItemResolver);
    }

    private Enemy? FindNearestEnemyInGuardRange(Vector2D origin)
    {
        Enemy? best = null;
        float bestDistSq = GUARD_DETECT_RADIUS_SQ;
        for (int i = 0; i < ContextEnemies.Count; i++)
        {
            Enemy e = ContextEnemies[i];
            if (!e.IsAlive)
            {
                continue;
            }

            float distSq = Vector2D.DistanceSquared(origin, e.Center);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = e;
            }
        }

        return best;
    }
}
