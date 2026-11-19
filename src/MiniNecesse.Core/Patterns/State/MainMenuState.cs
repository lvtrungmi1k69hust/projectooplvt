using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Patterns.State;

/// <summary>
/// GoF Concrete State: main menu state active prior to starting or resuming gameplay.
/// </summary>
public sealed class MainMenuState : IGameState
{
    private readonly Action? _onEnter;
    private readonly Action? _onExit;

    /// <inheritdoc />
    public GameState StateType => GameState.MainMenu;

    /// <summary>Initializes a new instance of the <see cref="MainMenuState"/> class.</summary>
    public MainMenuState(Action? onEnter = null, Action? onExit = null)
    {
        _onEnter = onEnter;
        _onExit = onExit;
    }

    /// <inheritdoc />
    public void Enter() => _onEnter?.Invoke();

    /// <inheritdoc />
    public void Update(float deltaTime)
    {
        // Simulation stays idle on the main menu.
    }

    /// <inheritdoc />
    public void Exit() => _onExit?.Invoke();
}
