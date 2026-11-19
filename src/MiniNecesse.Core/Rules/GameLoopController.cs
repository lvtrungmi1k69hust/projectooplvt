using System;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Fixed-timestep simulation loop controller ensuring deterministic physics and AI stepping at 60 Hz
/// (FIXED_DELTA_TIME = 1 / 60s) regardless of rendering frame rate, with zero per-frame allocations.
/// </summary>
public sealed class GameLoopController
{
    /// <summary>Canonical fixed simulation time step (1/60 second).</summary>
    public const float FIXED_DELTA_TIME = 1f / 60f;

    /// <summary>Maximum frame delta accepted per call to prevent spiral-of-death lag spikes.</summary>
    public const float MAX_FRAME_DELTA = 0.25f;

    private readonly Action<float> _fixedUpdate;
    private readonly float _fixedDeltaTime;
    private float _accumulator;
    private long _totalFixedSteps;

    /// <summary>Gets the configured fixed simulation step duration in seconds.</summary>
    public float FixedDeltaTime => _fixedDeltaTime;

    /// <summary>Gets residual unconsumed accumulator time in seconds (useful for render interpolation).</summary>
    public float Accumulator => _accumulator;

    /// <summary>Gets interpolation factor alpha in [0, 1) between the previous and next fixed tick.</summary>
    public float InterpolationAlpha => _accumulator / _fixedDeltaTime;

    /// <summary>Gets the total number of fixed updates executed since creation or reset.</summary>
    public long TotalFixedSteps => _totalFixedSteps;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameLoopController"/> class.
    /// </summary>
    public GameLoopController(Action<float> fixedUpdate, float fixedDeltaTime = FIXED_DELTA_TIME)
    {
        ArgumentNullException.ThrowIfNull(fixedUpdate);
        if (fixedDeltaTime <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime), "Fixed delta time must be positive.");
        }

        _fixedUpdate = fixedUpdate;
        _fixedDeltaTime = fixedDeltaTime;
    }

    /// <summary>
    /// Accumulates <paramref name="frameDeltaTime"/> (clamped to <see cref="MAX_FRAME_DELTA"/>)
    /// and executes as many fixed simulation steps as fit.
    /// </summary>
    /// <returns>The number of fixed ticks executed during this frame.</returns>
    public int StepFrame(float frameDeltaTime)
    {
        if (frameDeltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(frameDeltaTime), "Frame delta time cannot be negative.");
        }

        float clampedDelta = MathF.Min(frameDeltaTime, MAX_FRAME_DELTA);
        _accumulator += clampedDelta;

        int stepsExecuted = 0;
        while (_accumulator >= _fixedDeltaTime)
        {
            _fixedUpdate(_fixedDeltaTime);
            _accumulator -= _fixedDeltaTime;
            _totalFixedSteps++;
            stepsExecuted++;
        }

        return stepsExecuted;
    }

    /// <summary>Resets accumulated fractional time and step counters.</summary>
    public void Reset()
    {
        _accumulator = 0f;
        _totalFixedSteps = 0;
    }
}
