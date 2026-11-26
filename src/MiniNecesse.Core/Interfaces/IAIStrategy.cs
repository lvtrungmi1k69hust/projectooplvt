namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Strategy pattern contract defining runtime-swappable AI behavior for enemies and bosses.
/// </summary>
public interface IAIStrategy : IActorAIStrategy<Entities.Enemy>
{
}
