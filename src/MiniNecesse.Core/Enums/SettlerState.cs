namespace MiniNecesse.Core.Enums;

/// <summary>Finite state machine states governing friendly settler NPC behavior.</summary>
public enum SettlerState
{
    Idle = 0,
    Patrol = 1,
    Work = 2,
    ReturnHome = 3,
    Flee = 4
}
