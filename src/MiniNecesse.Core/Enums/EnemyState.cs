namespace MiniNecesse.Core.Enums;

/// <summary>
/// Represents the finite state machine (FSM) states of an enemy or boss.
/// </summary>
public enum EnemyState
{
    /// <summary>Stationary waiting state.</summary>
    Idle = 0,

    /// <summary>Wandering around the island when the player is out of detection range.</summary>
    Patrol = 1,

    /// <summary>Actively pursuing the player using pathfinding.</summary>
    Chase = 2,

    /// <summary>Within range and executing melee or ranged attacks.</summary>
    Attack = 3,

    /// <summary>Enraged boss phase triggered below critical health threshold.</summary>
    Enraged = 4,

    /// <summary>Defeated and pending removal or cleanup.</summary>
    Dead = 5
}
