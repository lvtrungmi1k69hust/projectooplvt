using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Audio;

/// <summary>
/// Observer-driven audio subsystem subscribing to <see cref="GameSession.OnSoundEffectRequested"/>.
/// Supports mute toggle, volume control, optional relative <c>Assets/Audio/*.wav</c> files,
/// and a safe fallback when audio assets are absent.
/// </summary>
public sealed class AudioManager : IDisposable
{
    private readonly string _audioDirectory;
    private readonly Dictionary<string, SoundPlayer> _loadedPlayers = new(StringComparer.OrdinalIgnoreCase);
    private GameSession? _attachedSession;
    private bool _isMuted;
    private int _volume = 80;
    private bool _disposed;

    /// <summary>Gets or sets whether audio output is muted.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    /// <summary>Gets or sets the master volume percentage clamped to [0, 100].</summary>
    public int Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0, 100);
    }

    /// <summary>Initializes a new instance of the <see cref="AudioManager"/> class.</summary>
    public AudioManager(string? audioDirectory = null)
    {
        _audioDirectory = string.IsNullOrWhiteSpace(audioDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Assets", "Audio")
            : audioDirectory;
    }

    /// <summary>Subscribes to <see cref="GameSession.OnSoundEffectRequested"/> cleanly.</summary>
    public void AttachToSession(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        DetachFromSession();
        _attachedSession = session;
        _attachedSession.OnSoundEffectRequested += HandleSoundRequested;
    }

    /// <summary>Unsubscribes cleanly from any attached <see cref="GameSession"/> to prevent memory leaks.</summary>
    public void DetachFromSession()
    {
        if (_attachedSession is not null)
        {
            _attachedSession.OnSoundEffectRequested -= HandleSoundRequested;
            _attachedSession = null;
        }
    }

    /// <summary>Plays a logical sound cue if unmuted and volume &gt; 0, falling back safely if no WAV file exists.</summary>
    public void PlayCue(string cueKey)
    {
        if (_disposed || _isMuted || _volume <= 0 || string.IsNullOrWhiteSpace(cueKey))
        {
            return;
        }

        if (TryPlayWavFile(cueKey))
        {
            return;
        }

        PlaySafeFallbackCue(cueKey);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DetachFromSession();
        foreach (SoundPlayer player in _loadedPlayers.Values)
        {
            player.Dispose();
        }

        _loadedPlayers.Clear();
        _disposed = true;
    }

    private void HandleSoundRequested(string cueKey) => PlayCue(cueKey);

    private bool TryPlayWavFile(string cueKey)
    {
        if (_loadedPlayers.TryGetValue(cueKey, out SoundPlayer? cachedPlayer))
        {
            return TryInvokePlay(cachedPlayer, cueKey);
        }

        string wavPath = Path.Combine(_audioDirectory, $"{cueKey}.wav");
        if (!File.Exists(wavPath))
        {
            return false;
        }

        try
        {
            var player = new SoundPlayer(wavPath);
            player.Load();
            _loadedPlayers[cueKey] = player;
            return TryInvokePlay(player, cueKey);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            Debug.WriteLine($"AudioManager WAV fallback for '{cueKey}': {ex.Message}");
            return false;
        }
    }

    private static bool TryInvokePlay(SoundPlayer player, string cueKey)
    {
        try
        {
            player.Play();
            return true;
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"AudioManager playback skipped for '{cueKey}': {ex.Message}");
            return false;
        }
    }

    private static void PlaySafeFallbackCue(string cueKey)
    {
        if (string.Equals(cueKey, "victory", StringComparison.OrdinalIgnoreCase))
        {
            SystemSounds.Asterisk.Play();
        }
        else if (string.Equals(cueKey, "game_over", StringComparison.OrdinalIgnoreCase))
        {
            SystemSounds.Hand.Play();
        }
        else
        {
            Debug.WriteLine($"[Audio Cue] {cueKey} (no .wav asset present — silent fallback)");
        }
    }
}
