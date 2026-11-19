using System.Collections.Generic;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Tests.Mocks;

/// <summary>
/// Spy observer implementing <see cref="IHudObserver"/> to verify Core event notifications without any UI.
/// </summary>
public sealed class SpyHudObserver : IHudObserver
{
    public int LastHp { get; private set; }
    public int LastMaxHp { get; private set; }
    public int LastScore { get; private set; }
    public int InventoryUpdateCount { get; private set; }
    public int LastDayNumber { get; private set; }
    public DayPhase LastDayPhase { get; private set; }
    public List<GameState> ObservedStates { get; } = new();
    public string LastBossName { get; private set; } = string.Empty;
    public bool BossBarVisible { get; private set; }

    public void OnPlayerHealthUpdated(int currentHp, int maxHp)
    {
        LastHp = currentHp;
        LastMaxHp = maxHp;
    }

    public void OnScoreUpdated(int newScore) => LastScore = newScore;

    public void OnInventoryUpdated() => InventoryUpdateCount++;

    public void OnDayPhaseUpdated(int dayNumber, DayPhase phase, float normalizedProgress)
    {
        LastDayNumber = dayNumber;
        LastDayPhase = phase;
    }

    public void OnGameStateUpdated(GameState newState) => ObservedStates.Add(newState);

    public void OnBossHealthUpdated(string bossName, int currentHp, int maxHp, bool isVisible)
    {
        LastBossName = bossName;
        BossBarVisible = isVisible;
    }
}
