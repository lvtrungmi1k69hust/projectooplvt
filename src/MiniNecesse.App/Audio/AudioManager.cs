using System;
using System.IO;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Audio;

/// <summary>
/// Observer-driven real-time audio subsystem combining <see cref="ProceduralSoundBank"/>
/// and <see cref="WaveOutAudioEngine"/>. Plays looping map music (Day/Night/Boss) automatically
/// on session attach and synthesizes distinct sound effects per tool, weapon, and action,
/// while supporting optional drop-in <c>Assets/Audio/*.wav</c> overrides.
/// </summary>
public sealed class AudioManager : IDisposable
{
    private readonly ProceduralSoundBank _soundBank;
    private readonly WaveOutAudioEngine _engine;

    private GameSession? _attachedSession;
    private string? _activeBgmKey;
    private bool _bossBgmActive;
    private bool _isMuted;
    private int _volume = 80;
    private bool _disposed;

    /// <summary>Gets or sets whether audio output is muted live.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            _engine.IsMuted = value;
        }
    }

    /// <summary>Gets or sets the master volume percentage clamped to [0, 100].</summary>
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _engine.Volume = _volume;
        }
    }

    /// <summary>Gets the active looping BGM cue key (e.g. <c>bgm_day</c>, <c>bgm_night</c>, <c>bgm_boss</c>), or <see langword="null"/> if stopped.</summary>
    public string? ActiveBgmKey => _activeBgmKey;

    /// <summary>Gets whether the Win32 audio output device opened cleanly on this host.</summary>
    public bool IsAudioDeviceReady => _engine.IsDeviceAvailable;

    /// <summary>Initializes a new instance of the <see cref="AudioManager"/> class.</summary>
    public AudioManager(string? audioDirectory = null)
    {
        string resolvedDir = string.IsNullOrWhiteSpace(audioDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Assets", "Audio")
            : audioDirectory;

        _soundBank = new ProceduralSoundBank(resolvedDir);
        _engine = new WaveOutAudioEngine
        {
            IsMuted = _isMuted,
            Volume = _volume
        };
    }

    /// <summary>Subscribes to session SFX, day/night transitions, and game state changes, starting map BGM immediately.</summary>
    public void AttachToSession(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        DetachFromSession();

        _attachedSession = session;
        _attachedSession.OnSoundEffectRequested += HandleSoundRequested;
        _attachedSession.DayNightCycle.OnDayPhaseChanged += HandleDayPhaseChanged;
        _attachedSession.OnGameStateChanged += HandleGameStateChanged;

        _bossBgmActive = _attachedSession.ActiveBoss is { IsAlive: true };
        PlayBgm(ResolveExpectedBgmKey(_attachedSession));
    }

    /// <summary>Unsubscribes cleanly from any attached <see cref="GameSession"/> and stops looping music.</summary>
    public void DetachFromSession()
    {
        if (_attachedSession is not null)
        {
            _attachedSession.OnSoundEffectRequested -= HandleSoundRequested;
            _attachedSession.DayNightCycle.OnDayPhaseChanged -= HandleDayPhaseChanged;
            _attachedSession.OnGameStateChanged -= HandleGameStateChanged;
            _attachedSession = null;
        }

        _bossBgmActive = false;
        StopBgm();
    }

    /// <summary>Starts or switches the looping background music track cleanly if not already playing.</summary>
    public void PlayBgm(string bgmKey)
    {
        if (_disposed || string.IsNullOrWhiteSpace(bgmKey))
        {
            return;
        }

        if (string.Equals(_activeBgmKey, bgmKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        short[]? clip = _soundBank.GetClip(bgmKey);
        if (clip is null)
        {
            return;
        }

        _activeBgmKey = bgmKey;
        _engine.PlayBgm(clip);
    }

    /// <summary>Stops the currently looping background music track.</summary>
    public void StopBgm()
    {
        _activeBgmKey = null;
        _engine.StopBgm();
    }

    /// <summary>Plays a logical sound cue if unmuted and volume &gt; 0, overlapping smoothly on the mixer.</summary>
    public void PlayCue(string cueKey)
    {
        if (_disposed || _isMuted || _volume <= 0 || string.IsNullOrWhiteSpace(cueKey))
        {
            return;
        }

        short[]? clip = _soundBank.GetClip(cueKey);
        if (clip is not null)
        {
            _engine.EnqueueSfx(clip);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DetachFromSession();
        _engine.Dispose();
        _disposed = true;
    }

    private void HandleSoundRequested(string cueKey)
    {
        if (string.Equals(cueKey, "boss_spawn", StringComparison.OrdinalIgnoreCase))
        {
            _bossBgmActive = true;
            PlayBgm("bgm_boss");
        }
        else if (string.Equals(cueKey, "boss_defeated", StringComparison.OrdinalIgnoreCase))
        {
            _bossBgmActive = false;
            if (_attachedSession is not null && _attachedSession.CurrentState == GameState.Playing)
            {
                PlayBgm(ResolveAmbientBgmKey(_attachedSession.DayNightCycle.CurrentPhase));
            }
        }

        PlayCue(cueKey);
    }

    private void HandleDayPhaseChanged(int dayNumber, DayPhase phase)
    {
        if (_attachedSession is null || _bossBgmActive || _attachedSession.CurrentState != GameState.Playing)
        {
            return;
        }

        PlayBgm(ResolveAmbientBgmKey(phase));
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState is GameState.Victory or GameState.GameOver or GameState.MainMenu)
        {
            StopBgm();
        }
        else if (newState == GameState.Playing && _attachedSession is not null)
        {
            PlayBgm(ResolveExpectedBgmKey(_attachedSession));
        }
    }

    private string ResolveExpectedBgmKey(GameSession session) =>
        _bossBgmActive ? "bgm_boss" : ResolveAmbientBgmKey(session.DayNightCycle.CurrentPhase);

    private static string ResolveAmbientBgmKey(DayPhase phase) =>
        phase == DayPhase.Night ? "bgm_night" : "bgm_day";
}
