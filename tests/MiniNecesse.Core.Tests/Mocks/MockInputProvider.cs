using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Tests.Mocks;

/// <summary>
/// Deterministic test double implementing <see cref="IInputProvider"/> for headless Core simulation tests
/// with zero dependency on WinForms or keyboard/mouse hardware.
/// </summary>
public sealed class MockInputProvider : IInputProvider
{
    public Vector2D MovementAxis { get; set; } = Vector2D.Zero;
    public Vector2D AimWorldPosition { get; set; } = Vector2D.Zero;
    public bool PrimaryActionPressed { get; set; }
    public bool SecondaryActionPressed { get; set; }
    public bool InteractPressed { get; set; }
    public bool PausePressed { get; set; }
    public bool InventoryTogglePressed { get; set; }
    public int? SelectedHotbarSlot { get; set; }

    public Vector2D GetMovementAxis() => MovementAxis;
    public Vector2D GetAimWorldPosition() => AimWorldPosition;
    public bool IsPrimaryActionPressed() => PrimaryActionPressed;
    public bool IsSecondaryActionPressed() => SecondaryActionPressed;
    public bool IsInteractPressed() => InteractPressed;
    public bool IsPausePressed() => PausePressed;
    public bool IsInventoryTogglePressed() => InventoryTogglePressed;
    public int? GetSelectedHotbarSlot() => SelectedHotbarSlot;
}
