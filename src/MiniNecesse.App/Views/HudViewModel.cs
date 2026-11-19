using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.App.Views;

/// <summary>
/// Presentation view model implementing <see cref="IHudObserver"/>.
/// Receives all HUD state strictly via Core observer notifications with zero domain logic.
/// </summary>
public sealed class HudViewModel : IHudObserver
{
    public int CurrentHp { get; private set; } = 100;
    public int MaxHp { get; private set; } = 100;
    public int Score { get; private set; }
    public int DayNumber { get; private set; } = 1;
    public DayPhase DayPhase { get; private set; } = DayPhase.Day;
    public float PhaseProgress { get; private set; }
    public GameState CurrentGameState { get; private set; } = GameState.MainMenu;
    public string BossName { get; private set; } = string.Empty;
    public int BossCurrentHp { get; private set; }
    public int BossMaxHp { get; private set; } = 1;
    public bool BossBarVisible { get; private set; }
    public int InventoryVersion { get; private set; }

    /// <summary>Raised on UI layer when inventory changed so Crafting panel refreshes lazily.</summary>
    public event Action? OnViewInventoryInvalidated;

    /// <summary>Raised on UI layer when observed game state changes (e.g., Victory/GameOver).</summary>
    public event Action<GameState>? OnViewGameStateChanged;

    /// <inheritdoc />
    public void OnPlayerHealthUpdated(int currentHp, int maxHp)
    {
        CurrentHp = currentHp;
        MaxHp = Math.Max(1, maxHp);
    }

    /// <inheritdoc />
    public void OnScoreUpdated(int newScore) => Score = newScore;

    /// <inheritdoc />
    public void OnInventoryUpdated()
    {
        InventoryVersion++;
        OnViewInventoryInvalidated?.Invoke();
    }

    /// <inheritdoc />
    public void OnDayPhaseUpdated(int dayNumber, DayPhase phase, float normalizedProgress)
    {
        DayNumber = dayNumber;
        DayPhase = phase;
        PhaseProgress = Math.Clamp(normalizedProgress, 0f, 1f);
    }

    /// <inheritdoc />
    public void OnGameStateUpdated(GameState newState)
    {
        CurrentGameState = newState;
        OnViewGameStateChanged?.Invoke(newState);
    }

    /// <inheritdoc />
    public void OnBossHealthUpdated(string bossName, int currentHp, int maxHp, bool isVisible)
    {
        BossName = bossName;
        BossCurrentHp = currentHp;
        BossMaxHp = Math.Max(1, maxHp);
        BossBarVisible = isVisible;
    }
}
