using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// GoF Factory Method (Creator Interface): defines factory operations to create domain entities
/// from data-driven blueprints without exposing concrete instantiation logic.
/// </summary>
public interface IEntityFactory
{
    /// <summary>Creates a configured <see cref="Enemy"/> scaled by <paramref name="difficulty"/>.</summary>
    Enemy CreateEnemy(string archetypeId, Vector2D spawnPosition, Difficulty difficulty = Difficulty.Normal);

    /// <summary>Creates a configured island <see cref="Boss"/> scaled by <paramref name="difficulty"/>.</summary>
    Boss CreateBoss(string bossId, Vector2D spawnPosition, Difficulty difficulty = Difficulty.Normal);

    /// <summary>Creates a configured island <see cref="Boss"/> using explicit JSON <see cref="BossConfiguration"/> stats.</summary>
    Boss CreateBoss(BossConfiguration bossConfig, Vector2D spawnPosition, Difficulty difficulty = Difficulty.Normal);

    /// <summary>Creates a configured harvestable <see cref="ResourceNode"/> at <paramref name="position"/>.</summary>
    ResourceNode CreateResourceNode(ResourceType resourceType, Vector2D position);

    /// <summary>Creates a configured placeable <see cref="Structure"/> snapped to (<paramref name="tileCol"/>, <paramref name="tileRow"/>).</summary>
    Structure CreateStructure(StructureType structureType, int tileCol, int tileRow, float tileSize = 32f);

    /// <summary>Creates a configured passive <see cref="Animal"/> at <paramref name="spawnPosition"/>.</summary>
    Animal CreateAnimal(AnimalType animalType, Vector2D spawnPosition);
}
