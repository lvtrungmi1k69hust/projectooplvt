using MiniNecesse.Core.AI;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Tests;

public sealed class CollisionAndPathfindingTests
{
    [Fact]
    public void TryDetectOverlap_OverlappingAabbEntities_ReturnsTrueWithExactSeparationAndSpatialQuery()
    {
        var detector = new CollisionDetector();
        var grid = new SpatialHashGrid<Entity>(cellSize: 64f);
        var queryBuffer = new List<Entity>();

        var player = new Player("player_1", new Vector2D(100f, 100f));
        var nearbyEnemy = new Enemy("en_near", "Slime", new Vector2D(116f, 104f), 30, 60f, 5, 24f, 1f);
        var farEnemy = new Enemy("en_far", "Slime", new Vector2D(500f, 500f), 30, 60f, 5, 24f, 1f);

        grid.Insert(nearbyEnemy);
        grid.Insert(farEnemy);
        grid.Query(player.GetBounds(), queryBuffer);

        Assert.Contains(nearbyEnemy, queryBuffer);
        Assert.DoesNotContain(farEnemy, queryBuffer);

        bool overlapped = detector.TryDetectOverlap(player, nearbyEnemy, out CollisionInfo info);

        Assert.True(overlapped);
        Assert.Equal(8f, info.PenetrationDepth, precision: 3);
        Assert.Equal(new Vector2D(-1f, 0f), info.SeparationNormal);
    }

    [Fact]
    public void FindPath_AroundWallObstacle_ReturnsShortestWalkablePath()
    {
        var level = new IslandLevel("ISLAND_ASTAR", "Path Test", "Forest", "KingSlime", 10, 10, 2, 5);
        // Build vertical solid barrier at col=5, rows 3..7 forcing path around row 2 or row 8.
        for (int row = 3; row <= 7; row++)
        {
            level.SetStructureOccupancy(5, row, true);
        }

        var pathfinder = new AStarPathfinder();
        List<GridPoint> path = pathfinder.FindPath(level, new GridPoint(3, 5), new GridPoint(7, 5), out PathResult result);

        Assert.True(result.ReachedGoal);
        Assert.Equal(new GridPoint(3, 5), path[0]);
        Assert.Equal(new GridPoint(7, 5), path[^1]);
        Assert.All(path, step => Assert.True(level.IsTileWalkable(step.Col, step.Row)));
        Assert.Equal(11, path.Count); // Shortest detour: 4 cols + 3 up + 3 down = 10 moves = 11 waypoints
    }

    [Fact]
    public void FindPath_WhenGoalCompletelyWalledIn_ReturnsFallbackBlockingWallWithoutHanging()
    {
        var level = new IslandLevel("ISLAND_WALLED", "Fortress Test", "Forest", "KingSlime", 10, 10, 5, 5);
        // Completely surround the goal tile (5, 5) with destructible walls.
        level.SetStructureOccupancy(5, 4, true);
        level.SetStructureOccupancy(5, 6, true);
        level.SetStructureOccupancy(4, 5, true);
        level.SetStructureOccupancy(6, 5, true);

        var pathfinder = new AStarPathfinder();
        List<GridPoint> path = pathfinder.FindPath(level, new GridPoint(2, 5), new GridPoint(5, 5), out PathResult result, maxIterations: 300);

        Assert.False(result.ReachedGoal);
        Assert.True(result.HasBlockingWall);
        Assert.Equal(new GridPoint(4, 5), result.BlockingWallTile);
        Assert.NotEmpty(path);
    }
}
