using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.State;

/// <summary>
/// GoF Concrete State: island victory state triggered after defeating the island boss.
/// </summary>
public sealed class VictoryState : IGameState
{
    private readonly Action? _onEnter;
    private readonly Action? _onExit;

    /// <inheritdoc />
    public GameState StateType => GameState.Victory;

    /// <summary>Initializes a new instance of the <see cref="VictoryState"/> class.</summary>
    public VictoryState(Action? onEnter = null, Action? onExit = null)
    {
        _onEnter = onEnter;
        _onExit = onExit;
    }

    /// <inheritdoc />
    public void Enter() => _onEnter?.Invoke();

    /// <inheritdoc />
    public void Update(float deltaTime)
    {
        // Simulation stops once island victory is achieved.
    }

    /// <inheritdoc />
    public void Exit() => _onExit?.Invoke();
}
