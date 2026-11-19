namespace MiniNecesse.Core.Enums;

/// <summary>
/// Represents the high-level lifecycle states of the game state machine.
/// </summary>
public enum GameState
{
    /// <summary>Main menu screen before entering gameplay.</summary>
    MainMenu = 0,

    /// <summary>Active top-down survival gameplay.</summary>
    Playing = 1,

    /// <summary>Gameplay temporarily suspended via pause menu.</summary>
    Paused = 2,

    /// <summary>Current island boss defeated and island cleared.</summary>
    Victory = 3,

    /// <summary>Player defeated when health reaches zero.</summary>
    GameOver = 4
}
