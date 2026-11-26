using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.World;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Coordinates deterministic island weather transitions synchronized with <see cref="DayNightCycle"/>
/// phase and day changes without UI dependencies.
/// </summary>
public sealed class WeatherSystem : IDisposable
{
    private readonly DayNightCycle _dayNightCycle;
    private readonly int _seed;
    private WeatherType _currentWeather = WeatherType.Clear;
    private float _intensity;
    private bool _disposed;

    /// <summary>Gets the active weather condition.</summary>
    public WeatherType CurrentWeather => _currentWeather;

    /// <summary>Gets normalized weather intensity in [0, 1] (0 when Clear).</summary>
    public float Intensity => _intensity;

    /// <summary>Raised whenever active weather changes.</summary>
    public event Action<WeatherType>? OnWeatherChanged;

    /// <summary>Initializes a new instance of the <see cref="WeatherSystem"/> class and subscribes to <paramref name="dayNightCycle"/>.</summary>
    public WeatherSystem(DayNightCycle dayNightCycle, int seed = 101)
    {
        ArgumentNullException.ThrowIfNull(dayNightCycle);
        _dayNightCycle = dayNightCycle;
        _seed = seed;
        _dayNightCycle.OnDayPhaseChanged += HandleDayPhaseChanged;
    }

    /// <summary>Restores persisted weather state from a save snapshot.</summary>
    public void RestoreWeather(WeatherType weather, float intensity = 0.65f)
    {
        _currentWeather = weather;
        _intensity = weather == WeatherType.Clear ? 0f : global::System.Math.Clamp(intensity, 0.25f, 1f);
        OnWeatherChanged?.Invoke(_currentWeather);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _dayNightCycle.OnDayPhaseChanged -= HandleDayPhaseChanged;
        _disposed = true;
    }

    private void HandleDayPhaseChanged(int dayNumber, DayPhase phase)
    {
        uint hash = DeterministicNoise.MixHash(dayNumber, (int)phase, _seed ^ 0x5A39D1);
        int roll = (int)(hash % 100u);

        WeatherType nextWeather = phase == DayPhase.Night
            ? (roll < 35 ? WeatherType.Clear : (roll < 65 ? WeatherType.Rain : WeatherType.Fog))
            : (roll < 55 ? WeatherType.Clear : (roll < 85 ? WeatherType.Rain : WeatherType.Fog));

        float nextIntensity = nextWeather == WeatherType.Clear
            ? 0f
            : 0.45f + (((hash >> 8) % 41u) / 100f);

        if (_currentWeather != nextWeather)
        {
            _currentWeather = nextWeather;
            _intensity = nextIntensity;
            OnWeatherChanged?.Invoke(_currentWeather);
        }
        else
        {
            _intensity = nextIntensity;
        }
    }
}
