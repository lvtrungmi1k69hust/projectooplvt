using System;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Settler strategy for Miners: moves near stone/ore veins during daytime and gathers ores into stockpile.
/// </summary>
public sealed class MinerSettlerStrategy : SettlerStrategyBase
{
    private const float MINERAL_SEARCH_RADIUS_SQ = 260f * 260f;
    private const float STOP_DISTANCE_SQ = 42f * 42f;

    /// <inheritdoc />
    public override string StrategyName => nameof(MinerSettlerStrategy);

    /// <inheritdoc />
    public override SettlerProfession Profession => SettlerProfession.Miner;

    /// <summary>Initializes a new instance of the <see cref="MinerSettlerStrategy"/> class.</summary>
    public MinerSettlerStrategy(
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
        ResourceNode? vein = FindNearestMineralNode(settler.Center);
        if (vein is not null)
        {
            float distSq = Vector2D.DistanceSquared(settler.Center, vein.Center);
            if (distSq > STOP_DISTANCE_SQ)
            {
                NavigateToward(settler, vein.Center, level, deltaTime);
            }
        }
        else if (settler.HomeTile.HasValue)
        {
            Vector2D homeWorld = level.TileToWorldCenter(settler.HomeTile.Value.Col, settler.HomeTile.Value.Row);
            if (Vector2D.DistanceSquared(settler.Center, homeWorld) > 64f * 64f)
            {
                NavigateToward(settler, homeWorld, level, deltaTime);
            }
        }

        settler.TryAdvanceProduction(deltaTime, ItemResolver);
    }

    private ResourceNode? FindNearestMineralNode(Vector2D origin)
    {
        ResourceNode? best = null;
        float bestDistSq = MINERAL_SEARCH_RADIUS_SQ;
        for (int i = 0; i < ContextResources.Count; i++)
        {
            ResourceNode node = ContextResources[i];
            if (!node.IsAlive || node.RequiredTool != ToolType.Pickaxe)
            {
                continue;
            }

            float distSq = Vector2D.DistanceSquared(origin, node.Center);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = node;
            }
        }

        return best;
    }
}
