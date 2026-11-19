using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Patterns.Flyweight;

/// <summary>
/// GoF Flyweight Factory: caches and returns shared <see cref="TileDataFlyweight"/> instances
/// keyed by <see cref="TileType"/> so every map tile shares the exact same intrinsic metadata object.
/// </summary>
public sealed class TileDataFlyweightFactory
{
    private static readonly Lazy<TileDataFlyweightFactory> SHARED_INSTANCE = new(CreateDefault);

    private readonly Dictionary<TileType, TileDataFlyweight> _cache = new();

    /// <summary>Gets the default shared flyweight factory populated with standard island biomes.</summary>
    public static TileDataFlyweightFactory Shared => SHARED_INSTANCE.Value;

    /// <summary>Gets the number of cached distinct flyweight instances.</summary>
    public int CachedCount => _cache.Count;

    /// <summary>Registers or updates a shared flyweight definition for <paramref name="flyweight"/>'s <see cref="TileType"/>.</summary>
    public void Register(TileDataFlyweight flyweight)
    {
        ArgumentNullException.ThrowIfNull(flyweight);
        _cache[flyweight.TileType] = flyweight;
    }

    /// <summary>
    /// Retrieves the shared <see cref="TileDataFlyweight"/> for <paramref name="tileType"/>.
    /// Complexity: O(1) dictionary lookup.
    /// </summary>
    public TileDataFlyweight Get(TileType tileType)
    {
        if (_cache.TryGetValue(tileType, out TileDataFlyweight? flyweight))
        {
            return flyweight;
        }

        throw new KeyNotFoundException($"No TileDataFlyweight registered for TileType '{tileType}'.");
    }

    /// <summary>Creates a new factory preloaded with all default biome tile definitions.</summary>
    public static TileDataFlyweightFactory CreateDefault()
    {
        var factory = new TileDataFlyweightFactory();
        factory.Register(new TileDataFlyweight(TileType.Grass, hardness: 1, isWalkable: true, movementSpeedMultiplier: 1.00f, colorHex: "#4E9A3F", spriteId: "tile_grass"));
        factory.Register(new TileDataFlyweight(TileType.Sand, hardness: 1, isWalkable: true, movementSpeedMultiplier: 0.94f, colorHex: "#DEC47C", spriteId: "tile_sand"));
        factory.Register(new TileDataFlyweight(TileType.Water, hardness: 0, isWalkable: false, movementSpeedMultiplier: 0.00f, colorHex: "#2B78B7", spriteId: "tile_water"));
        factory.Register(new TileDataFlyweight(TileType.Stone, hardness: 3, isWalkable: true, movementSpeedMultiplier: 1.00f, colorHex: "#7A8288", spriteId: "tile_stone"));
        factory.Register(new TileDataFlyweight(TileType.Snow, hardness: 1, isWalkable: true, movementSpeedMultiplier: 0.88f, colorHex: "#E3EEF5", spriteId: "tile_snow"));
        factory.Register(new TileDataFlyweight(TileType.Swamp, hardness: 1, isWalkable: true, movementSpeedMultiplier: 0.72f, colorHex: "#495E38", spriteId: "tile_swamp"));
        factory.Register(new TileDataFlyweight(TileType.DarkStone, hardness: 4, isWalkable: true, movementSpeedMultiplier: 1.00f, colorHex: "#3C354A", spriteId: "tile_darkstone"));
        return factory;
    }
}
