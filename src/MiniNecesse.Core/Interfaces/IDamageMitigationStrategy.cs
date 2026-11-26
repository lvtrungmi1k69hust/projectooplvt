namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Strategy pattern contract defining how player armor defense and rules scale incoming raw damage (OCP).
/// </summary>
public interface IDamageMitigationStrategy
{
    /// <summary>Computes final HP damage taken after applying <paramref name="totalDefense"/>.</summary>
    int ComputeFinalDamage(int rawDamage, int totalDefense);
}
