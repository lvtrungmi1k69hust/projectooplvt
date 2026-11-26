using MiniNecesse.Core.Entities;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Generic AI strategy contract shared across enemies and friendly settlers (OCP &amp; LSP).
/// </summary>
public interface IActorAIStrategy<in TActor>
{
    /// <summary>Gets a descriptive name identifying this AI strategy.</summary>
    string StrategyName { get; }

    /// <summary>Executes one fixed-timestep AI update for <paramref name="actor"/>.</summary>
    void Execute(TActor actor, Player player, IslandLevel level, float deltaTime);
}
