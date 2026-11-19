using MiniNecesse.Core.Entities;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Strategy pattern contract defining runtime-swappable AI behavior for enemies and bosses.
/// </summary>
public interface IAIStrategy
{
    /// <summary>Gets a descriptive name identifying this AI strategy.</summary>
    string StrategyName { get; }

    /// <summary>Executes one fixed-timestep AI decision and movement update for <paramref name="enemy"/>.</summary>
    /// <param name="enemy">The controlled enemy or boss.</param>
    /// <param name="target">The player target.</param>
    /// <param name="level">The current island tilemap for navigation.</param>
    /// <param name="deltaTime">Fixed simulation time step in seconds.</param>
    void Execute(Enemy enemy, Player target, IslandLevel level, float deltaTime);
}
