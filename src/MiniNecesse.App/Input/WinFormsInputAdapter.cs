using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.App.Input;

/// <summary>
/// WinForms presentation adapter implementing <see cref="IInputProvider"/> (DIP).
/// Translates keyboard and mouse events into platform-agnostic Core input queries
/// using configurable <see cref="KeyBindingConfig"/>.
/// </summary>
public sealed class WinFormsInputAdapter : IInputProvider
{
    private readonly KeyBindingConfig _bindings;
    private readonly HashSet<Keys> _heldKeys = new();

    private Point _mouseScreenPosition;
    private Vector2D _cameraWorldOffset;
    private bool _leftMouseHeld;
    private bool _rightMouseQueued;
    private bool _interactQueued;
    private bool _pauseQueued;
    private bool _craftingQueued;
    private int? _selectedHotbarQueued;

    /// <summary>Gets the active key binding configuration.</summary>
    public KeyBindingConfig Bindings => _bindings;

    /// <summary>Initializes a new instance of the <see cref="WinFormsInputAdapter"/> class.</summary>
    public WinFormsInputAdapter(KeyBindingConfig? bindings = null)
    {
        _bindings = bindings ?? KeyBindingConfig.LoadOrDefault();
    }

    /// <summary>Updates the current camera top-left world offset for screen-to-world mouse mapping.</summary>
    public void UpdateCameraOffset(Vector2D cameraWorldOffset) => _cameraWorldOffset = cameraWorldOffset;

    /// <summary>Records a WinForms KeyDown event.</summary>
    public void HandleKeyDown(Keys keyCode)
    {
        bool firstPress = _heldKeys.Add(keyCode);
        if (!firstPress)
        {
            return;
        }

        if (keyCode == _bindings.Pause)
        {
            _pauseQueued = true;
            return;
        }

        if (keyCode == _bindings.ToggleCrafting || keyCode == Keys.Tab)
        {
            _craftingQueued = true;
            return;
        }

        if (keyCode == _bindings.Interact)
        {
            _interactQueued = true;
            return;
        }

        TryQueueHotbarSelection(keyCode);
    }

    /// <summary>Records a WinForms KeyUp event.</summary>
    public void HandleKeyUp(Keys keyCode) => _heldKeys.Remove(keyCode);

    /// <summary>Records a WinForms MouseDown event.</summary>
    public void HandleMouseDown(MouseButtons button, Point location)
    {
        _mouseScreenPosition = location;
        if (button == MouseButtons.Left)
        {
            _leftMouseHeld = true;
        }
        else if (button == MouseButtons.Right)
        {
            _rightMouseQueued = true;
        }
    }

    /// <summary>Records a WinForms MouseUp event.</summary>
    public void HandleMouseUp(MouseButtons button)
    {
        if (button == MouseButtons.Left)
        {
            _leftMouseHeld = false;
        }
    }

    /// <summary>Records a WinForms MouseMove event.</summary>
    public void HandleMouseMove(Point location) => _mouseScreenPosition = location;

    /// <summary>Clears all held key and mouse states (e.g., when focus is lost or overlay opens).</summary>
    public void ClearAllInputs()
    {
        _heldKeys.Clear();
        _leftMouseHeld = false;
        _rightMouseQueued = false;
        _interactQueued = false;
        _pauseQueued = false;
        _craftingQueued = false;
        _selectedHotbarQueued = null;
    }

    /// <inheritdoc />
    public Vector2D GetMovementAxis()
    {
        float x = 0f;
        float y = 0f;

        if (_heldKeys.Contains(_bindings.MoveLeft) || _heldKeys.Contains(Keys.Left)) x -= 1f;
        if (_heldKeys.Contains(_bindings.MoveRight) || _heldKeys.Contains(Keys.Right)) x += 1f;
        if (_heldKeys.Contains(_bindings.MoveUp) || _heldKeys.Contains(Keys.Up)) y -= 1f;
        if (_heldKeys.Contains(_bindings.MoveDown) || _heldKeys.Contains(Keys.Down)) y += 1f;

        return new Vector2D(x, y).Normalized();
    }

    /// <inheritdoc />
    public Vector2D GetAimWorldPosition()
    {
        return new Vector2D(
            _mouseScreenPosition.X + _cameraWorldOffset.X,
            _mouseScreenPosition.Y + _cameraWorldOffset.Y);
    }

    /// <inheritdoc />
    public bool IsPrimaryActionPressed() => _leftMouseHeld;

    /// <inheritdoc />
    public bool IsSecondaryActionPressed()
    {
        bool queued = _rightMouseQueued;
        _rightMouseQueued = false;
        return queued;
    }

    /// <inheritdoc />
    public bool IsInteractPressed()
    {
        bool queued = _interactQueued;
        _interactQueued = false;
        return queued;
    }

    /// <inheritdoc />
    public bool IsPausePressed()
    {
        bool queued = _pauseQueued;
        _pauseQueued = false;
        return queued;
    }

    /// <inheritdoc />
    public bool IsInventoryTogglePressed()
    {
        bool queued = _craftingQueued;
        _craftingQueued = false;
        return queued;
    }

    /// <inheritdoc />
    public int? GetSelectedHotbarSlot()
    {
        int? slot = _selectedHotbarQueued;
        _selectedHotbarQueued = null;
        return slot;
    }

    private void TryQueueHotbarSelection(Keys keyCode)
    {
        for (int i = 0; i < _bindings.HotbarKeys.Length; i++)
        {
            if (keyCode == _bindings.HotbarKeys[i])
            {
                _selectedHotbarQueued = i;
                return;
            }
        }
    }
}
