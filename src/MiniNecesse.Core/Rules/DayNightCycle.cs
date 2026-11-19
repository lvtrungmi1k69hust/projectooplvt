using System;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Manages continuous in-game day/night progression and raises phase transition events.
/// </summary>
public sealed class DayNightCycle
{
    private readonly float _dayDurationSeconds;
    private readonly float _nightDurationSeconds;
    private int _dayNumber;
    private float _elapsedInCycle;
    private DayPhase _currentPhase;

    /// <summary>Gets the 1-based current day number.</summary>
    public int DayNumber => _dayNumber;

    /// <summary>Gets the active day or night phase.</summary>
    public DayPhase CurrentPhase => _currentPhase;

    /// <summary>Gets total duration of the daytime phase in seconds.</summary>
    public float DayDurationSeconds => _dayDurationSeconds;

    /// <summary>Gets total duration of the nighttime phase in seconds.</summary>
    public float NightDurationSeconds => _nightDurationSeconds;

    /// <summary>Gets total duration of one full day/night cycle in seconds.</summary>
    public float FullCycleDuration => _dayDurationSeconds + _nightDurationSeconds;

    /// <summary>Gets elapsed seconds within the current full day/night cycle.</summary>
    public float ElapsedSecondsInCycle => _elapsedInCycle;

    /// <summary>Gets normalized progress within the active phase in [0, 1].</summary>
    public float NormalizedPhaseProgress => _currentPhase == DayPhase.Day
        ? _elapsedInCycle / _dayDurationSeconds
        : (_elapsedInCycle - _dayDurationSeconds) / _nightDurationSeconds;

    /// <summary>Raised when the cycle transitions between Day and Night, reporting (dayNumber, newPhase).</summary>
    public event Action<int, DayPhase>? OnDayPhaseChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DayNightCycle"/> class.
    /// </summary>
    public DayNightCycle(float dayDurationSeconds = 60f, float nightDurationSeconds = 45f)
    {
        if (dayDurationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(dayDurationSeconds), "Day duration must be positive.");
        }

        if (nightDurationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(nightDurationSeconds), "Night duration must be positive.");
        }

        _dayDurationSeconds = dayDurationSeconds;
        _nightDurationSeconds = nightDurationSeconds;
        _dayNumber = 1;
        _elapsedInCycle = 0f;
        _currentPhase = DayPhase.Day;
    }

    /// <summary>Advances the cycle clock by <paramref name="deltaTime"/> seconds.</summary>
    public void Advance(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (deltaTime == 0f)
        {
            return;
        }

        _elapsedInCycle += deltaTime;
        while (_elapsedInCycle >= FullCycleDuration)
        {
            _elapsedInCycle -= FullCycleDuration;
            _dayNumber++;
            TransitionPhaseIfChanged(DayPhase.Day);
        }

        DayPhase expectedPhase = _elapsedInCycle < _dayDurationSeconds ? DayPhase.Day : DayPhase.Night;
        TransitionPhaseIfChanged(expectedPhase);
    }

    /// <summary>Restores cycle state from a persisted snapshot.</summary>
    public void RestoreState(int dayNumber, float elapsedInCycle)
    {
        if (dayNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dayNumber), "Day number must be positive.");
        }

        if (elapsedInCycle < 0f || elapsedInCycle >= FullCycleDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedInCycle), "Elapsed cycle time is out of bounds.");
        }

        _dayNumber = dayNumber;
        _elapsedInCycle = elapsedInCycle;
        _currentPhase = _elapsedInCycle < _dayDurationSeconds ? DayPhase.Day : DayPhase.Night;
        OnDayPhaseChanged?.Invoke(_dayNumber, _currentPhase);
    }

    private void TransitionPhaseIfChanged(DayPhase newPhase)
    {
        if (_currentPhase == newPhase)
        {
            return;
        }

        _currentPhase = newPhase;
        OnDayPhaseChanged?.Invoke(_dayNumber, _currentPhase);
    }
}
