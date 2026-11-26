namespace MiniNecesse.Core.Tutorial;

/// <summary>
/// Gameplay events that advance a data-driven tutorial checklist step.
/// </summary>
public enum TutorialTriggerType
{
    HarvestResource = 0,
    CraftItem = 1,
    DamageEnemy = 2,
    PlaceStructure = 3
}
