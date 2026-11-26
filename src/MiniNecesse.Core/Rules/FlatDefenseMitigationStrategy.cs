using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Default survival damage mitigation strategy subtracting armor defense while enforcing
/// a minimum of 1 damage on positive hits so armored players still face threat.
/// </summary>
public sealed class FlatDefenseMitigationStrategy : IDamageMitigationStrategy
{
    /// <summary>Shared singleton instance avoiding extra allocations.</summary>
    public static FlatDefenseMitigationStrategy Instance { get; } = new();

    /// <inheritdoc />
    public int ComputeFinalDamage(int rawDamage, int totalDefense)
    {
        if (rawDamage <= 0)
        {
            return 0;
        }

        int reduced = rawDamage - global::System.Math.Max(0, totalDefense);
        return global::System.Math.Max(1, reduced);
    }
}
