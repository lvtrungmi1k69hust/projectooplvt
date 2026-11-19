using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Abstraction decoupling Core gameplay commands from any UI framework input API.
/// Implemented by WinForms/Unity adapters in Presentation and fakes in unit tests.
/// </summary>
public interface IInputProvider
{
    /// <summary>Returns the raw or normalized 2D movement input axis.</summary>
    Vector2D GetMovementAxis();

    /// <summary>Returns the current cursor/aim target position in world coordinates.</summary>
    Vector2D GetAimWorldPosition();

    /// <summary>Indicates whether the primary action (attack/harvest) is triggered.</summary>
    bool IsPrimaryActionPressed();

    /// <summary>Indicates whether the secondary action (build/use consumable) is triggered.</summary>
    bool IsSecondaryActionPressed();

    /// <summary>Indicates whether the interact / summon boss key is triggered.</summary>
    bool IsInteractPressed();

    /// <summary>Indicates whether the pause toggle key is triggered.</summary>
    bool IsPausePressed();

    /// <summary>Indicates whether the inventory/crafting overlay toggle key is triggered.</summary>
    bool IsInventoryTogglePressed();

    /// <summary>Returns the 0-based hotbar slot requested this frame, or <see langword="null"/> if unchanged.</summary>
    int? GetSelectedHotbarSlot();
}
