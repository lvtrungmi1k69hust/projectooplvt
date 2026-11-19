using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.State;

/// <summary>
/// GoF Concrete State: active gameplay state advancing fixed-timestep simulation ticks.
/// </summary>
public sealed class PlayingState : IGameState
{
    private readonly Action<float> _simulationStep;
    private readonly Action? _onEnter;
    private readonly Action? _onExit;

    /// <inheritdoc />
    public GameState StateType => GameState.Playing;

    /// <summary>Initializes a new instance of the <see cref="PlayingState"/> class.</summary>
    public PlayingState(Action<float> simulationStep, Action? onEnter = null, Action? onExit = null)
    {
        ArgumentNullException.ThrowIfNull(simulationStep);
        _simulationStep = simulationStep;
        _onEnter = onEnter;
        _onExit = onExit;
    }

    /// <inheritdoc />
    public void Enter() => _onEnter?.Invoke();

    /// <inheritdoc />
    public void Update(float deltaTime) => _simulationStep(deltaTime);

    /// <inheritdoc />
    public void Exit() => _onExit?.Invoke();
}
