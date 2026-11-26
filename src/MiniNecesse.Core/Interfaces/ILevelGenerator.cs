using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Procedural world generator abstraction (DIP / OCP) producing deterministic,
/// lazy-chunked multi-biome Endless worlds from a numeric seed without altering <see cref="ILevelLoader"/>.
/// </summary>
public interface ILevelGenerator
{
    /// <summary>Generates a deterministic world package for <paramref name="seed"/>.</summary>
    LoadedIslandPackage GenerateWorld(int seed, EndlessWorldOptions? options = null);
}
