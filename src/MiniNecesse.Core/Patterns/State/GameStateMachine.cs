using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.State;

/// <summary>
/// GoF State Pattern (Context): manages transitions across concrete <see cref="IGameState"/>
/// implementations without nested conditional state logic.
/// </summary>
public sealed class GameStateMachine
{
    private readonly Dictionary<GameState, IGameState> _states = new();
    private IGameState? _currentState;

    /// <summary>Gets the active concrete state object.</summary>
    public IGameState? Current => _currentState;

    /// <summary>Gets the enum identifier of the active state.</summary>
    public GameState CurrentStateType => _currentState?.StateType ?? GameState.MainMenu;

    /// <summary>Raised whenever a state transition completes.</summary>
    public event Action<GameState>? OnStateChanged;

    /// <summary>Registers a concrete <see cref="IGameState"/> implementation.</summary>
    public void RegisterState(IGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[state.StateType] = state;
    }

    /// <summary>Transitions cleanly from the current state (invoking Exit) to <paramref name="nextState"/> (invoking Enter).</summary>
    public void TransitionTo(GameState nextState)
    {
        if (!_states.TryGetValue(nextState, out IGameState? target))
        {
            throw new KeyNotFoundException($"Game state '{nextState}' is not registered in GameStateMachine.");
        }

        if (ReferenceEquals(_currentState, target))
        {
            return;
        }

        _currentState?.Exit();
        _currentState = target;
        _currentState.Enter();
        OnStateChanged?.Invoke(_currentState.StateType);
    }

    /// <summary>Delegates fixed-timestep simulation updates to the currently active <see cref="IGameState"/>.</summary>
    public void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        _currentState?.Update(deltaTime);
    }
}
