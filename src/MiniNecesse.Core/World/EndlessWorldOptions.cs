using MiniNecesse.Core.Entities;

namespace MiniNecesse.Core.World;

/// <summary>
/// Parameters controlling <see cref="SeededEndlessLevelGenerator"/> dimensions and biome catalog.
/// </summary>
public sealed record EndlessWorldOptions(
    int Width = 384,
    int Height = 384,
    int ChunkSize = IslandLevel.DEFAULT_CHUNK_SIZE,
    float TileSize = 32f,
    int SafeSpawnRadiusTiles = 8,
    float ResourceDensity = 0.032f,
    EndlessBiomeCatalog? BiomeCatalog = null);
