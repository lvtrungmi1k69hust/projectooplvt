using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.State;

/// <summary>
/// GoF Concrete State: suspended gameplay state halting world simulation while preserving session data.
/// </summary>
public sealed class PausedState : IGameState
{
    private readonly Action? _onEnter;
    private readonly Action? _onExit;

    /// <inheritdoc />
    public GameState StateType => GameState.Paused;

    /// <summary>Initializes a new instance of the <see cref="PausedState"/> class.</summary>
    public PausedState(Action? onEnter = null, Action? onExit = null)
    {
        _onEnter = onEnter;
        _onExit = onExit;
    }

    /// <inheritdoc />
    public void Enter() => _onEnter?.Invoke();

    /// <inheritdoc />
    public void Update(float deltaTime)
    {
        // World simulation is frozen while paused.
    }

    /// <inheritdoc />
    public void Exit() => _onExit?.Invoke();
}
