using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Validates enclosed player-built houses using bounded flood-fill BFS with reusable buffers
/// (zero heap allocations in steady state, deduplicates multi-door rooms).
/// </summary>
public sealed class HousingValidator
{
    public const int MIN_INTERIOR_TILES = 6;
    public const int MAX_INTERIOR_TILES = 120;

    private static readonly int[] DX = { 1, -1, 0, 0 };
    private static readonly int[] DY = { 0, 0, 1, -1 };

    private readonly Queue<GridPoint> _queue = new(128);
    private readonly HashSet<GridPoint> _visitedInterior = new();
    private readonly HashSet<GridPoint> _countedDoors = new();
    private readonly HashSet<GridPoint> _countedTorches = new();
    private readonly HashSet<GridPoint> _claimedInteriorTiles = new();

    /// <summary>Validates whether a room enclosing <paramref name="startTile"/> forms a valid house.</summary>
    public HouseValidationResult ValidateRoom(
        IslandLevel level,
        GridPoint startTile,
        Func<GridPoint, Structure?> structureLookup)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(structureLookup);

        if (!level.TryGetTile(startTile.Col, startTile.Row, out Tile? originTile) ||
            originTile is null ||
            !originTile.Flyweight.IsWalkable)
        {
            return new HouseValidationResult(false, 0, 0, 0, startTile, "Start tile is water or out of bounds.");
        }

        Structure? startStruct = structureLookup(startTile);
        if (startStruct is not null && (IsWallBoundary(startStruct.StructureType) || IsDoorStructure(startStruct)))
        {
            return new HouseValidationResult(false, 0, 0, 0, startTile, "Start tile cannot be inside a wall or door.");
        }

        _queue.Clear();
        _visitedInterior.Clear();
        _countedDoors.Clear();
        _countedTorches.Clear();

        _queue.Enqueue(startTile);
        _visitedInterior.Add(startTile);

        while (_queue.Count > 0)
        {
            GridPoint current = _queue.Dequeue();

            Structure? insideStruct = structureLookup(current);
            if (insideStruct is not null && IsLightSource(insideStruct))
            {
                _countedTorches.Add(current);
            }

            if (_visitedInterior.Count > MAX_INTERIOR_TILES)
            {
                return new HouseValidationResult(
                    false,
                    _visitedInterior.Count,
                    _countedDoors.Count,
                    _countedTorches.Count,
                    startTile,
                    "Room exceeds maximum enclosed interior size.");
            }

            for (int i = 0; i < 4; i++)
            {
                var next = new GridPoint(current.Col + DX[i], current.Row + DY[i]);
                if (!level.TryGetTile(next.Col, next.Row, out Tile? nextTile) || nextTile is null)
                {
                    return new HouseValidationResult(
                        false,
                        _visitedInterior.Count,
                        _countedDoors.Count,
                        _countedTorches.Count,
                        startTile,
                        "Room is not enclosed (reaches island edge).");
                }

                Structure? border = structureLookup(next);
                if (border is not null && border.IsAlive)
                {
                    if (IsLightSource(border))
                    {
                        _countedTorches.Add(next);
                    }

                    if (IsDoorStructure(border))
                    {
                        _countedDoors.Add(next);
                        continue;
                    }

                    if (IsWallBoundary(border.StructureType))
                    {
                        continue;
                    }
                }

                if (!nextTile.Flyweight.IsWalkable)
                {
                    return new HouseValidationResult(
                        false,
                        _visitedInterior.Count,
                        _countedDoors.Count,
                        _countedTorches.Count,
                        startTile,
                        "Room is not enclosed (leaks into water).");
                }

                if (_visitedInterior.Add(next))
                {
                    _queue.Enqueue(next);
                }
            }
        }

        int interiorCount = _visitedInterior.Count;
        int doorCount = _countedDoors.Count;
        int torchCount = _countedTorches.Count;

        if (doorCount < 1)
        {
            return new HouseValidationResult(false, interiorCount, doorCount, torchCount, startTile, "House requires at least 1 door.");
        }

        if (interiorCount < MIN_INTERIOR_TILES)
        {
            return new HouseValidationResult(false, interiorCount, doorCount, torchCount, startTile, $"House needs >= {MIN_INTERIOR_TILES} interior tiles.");
        }

        return new HouseValidationResult(true, interiorCount, doorCount, torchCount, startTile, "Valid house.");
    }

    /// <summary>
    /// Scans all door structures to discover every distinct valid enclosed house without double-counting multi-door rooms.
    /// </summary>
    public void FindAllValidHouses(
        IslandLevel level,
        IReadOnlyList<Structure> structures,
        Func<GridPoint, Structure?> structureLookup,
        List<HouseValidationResult> outputBuffer)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(structures);
        ArgumentNullException.ThrowIfNull(structureLookup);
        ArgumentNullException.ThrowIfNull(outputBuffer);

        outputBuffer.Clear();
        _claimedInteriorTiles.Clear();

        for (int i = 0; i < structures.Count; i++)
        {
            Structure s = structures[i];
            if (!s.IsAlive || !IsDoorStructure(s))
            {
                continue;
            }

            for (int dir = 0; dir < 4; dir++)
            {
                var candidate = new GridPoint(s.TileCol + DX[dir], s.TileRow + DY[dir]);
                if (_claimedInteriorTiles.Contains(candidate))
                {
                    continue;
                }

                HouseValidationResult result = ValidateRoom(level, candidate, structureLookup);
                if (result.IsValid)
                {
                    outputBuffer.Add(result);
                    foreach (GridPoint visited in _visitedInterior)
                    {
                        _claimedInteriorTiles.Add(visited);
                    }
                }
            }
        }
    }

    private static bool IsWallBoundary(StructureType type) =>
        type is StructureType.WoodenWall or StructureType.StoneWall or StructureType.WoodenFence;

    private static bool IsDoorStructure(Structure s) =>
        s.IsDoor || s.StructureType == StructureType.WoodenDoor;

    private static bool IsLightSource(Structure s) =>
        s.LightRadius > 0f || s.StructureType == StructureType.Torch;
}
