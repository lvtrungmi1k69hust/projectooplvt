using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.State;

/// <summary>
/// GoF Concrete State: game-over state triggered when player health drops to zero.
/// </summary>
public sealed class GameOverState : IGameState
{
    private readonly Action? _onEnter;
    private readonly Action? _onExit;

    /// <inheritdoc />
    public GameState StateType => GameState.GameOver;

    /// <summary>Initializes a new instance of the <see cref="GameOverState"/> class.</summary>
    public GameOverState(Action? onEnter = null, Action? onExit = null)
    {
        _onEnter = onEnter;
        _onExit = onExit;
    }

    /// <inheritdoc />
    public void Enter() => _onEnter?.Invoke();

    /// <inheritdoc />
    public void Update(float deltaTime)
    {
        // Simulation stops on game over.
    }

    /// <inheritdoc />
    public void Exit() => _onExit?.Invoke();
}
