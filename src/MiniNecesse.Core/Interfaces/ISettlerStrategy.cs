using System;
using System.Collections.Generic;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Strategy pattern contract driving settler AI occupation behavior (Miner, Hunter) via <see cref="IActorAIStrategy{TActor}"/>.
/// </summary>
public interface ISettlerStrategy : IActorAIStrategy<Settler>
{
    /// <summary>Gets the occupation archetype served by this strategy.</summary>
    SettlerProfession Profession { get; }

    /// <summary>Supplies current frame references to world actors and phase without per-tick heap allocations.</summary>
    void SetWorldContext(
        IReadOnlyList<Enemy> enemies,
        IReadOnlyList<ResourceNode> resources,
        DayPhase phase,
        Action<Vector2D, Vector2D, int, string>? projectileSpawner);
}
