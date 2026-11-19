using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// State pattern contract representing one lifecycle state inside the game state machine.
/// </summary>
public interface IGameState
{
    /// <summary>Gets the enum identifier of this state.</summary>
    GameState StateType { get; }

    /// <summary>Invoked once when transitioning into this state.</summary>
    void Enter();

    /// <summary>Invoked each fixed simulation step while this state is active.</summary>
    void Update(float deltaTime);

    /// <summary>Invoked once when transitioning away from this state.</summary>
    void Exit();
}
