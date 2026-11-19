using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Observer interface notified by Core gameplay rules whenever HUD-visible state changes.
/// </summary>
public interface IHudObserver
{
    /// <summary>Notifies that the player's health changed.</summary>
    void OnPlayerHealthUpdated(int currentHp, int maxHp);

    /// <summary>Notifies that the player's score changed.</summary>
    void OnScoreUpdated(int newScore);

    /// <summary>Notifies that inventory contents or selected hotbar slot changed.</summary>
    void OnInventoryUpdated();

    /// <summary>Notifies that the day/night cycle advanced or changed phase.</summary>
    void OnDayPhaseUpdated(int dayNumber, DayPhase phase, float normalizedProgress);

    /// <summary>Notifies that the overall game state transitioned.</summary>
    void OnGameStateUpdated(GameState newState);

    /// <summary>Notifies that the active island boss health bar should be updated or hidden.</summary>
    void OnBossHealthUpdated(string bossName, int currentHp, int maxHp, bool isVisible);
}
