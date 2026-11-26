using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MiniNecesse.App.Audio;

/// <summary>
/// Real-time Win32 <c>winmm.dll</c> <c>waveOut</c> PCM audio mixer capable of playing
/// 1 continuous looping BGM stream and up to 12 concurrent SFX voices with live volume/mute control.
/// Gracefully degrades to no-op if no audio output device is present.
/// </summary>
public sealed class WaveOutAudioEngine : IDisposable
{
    private const int BUFFER_COUNT = 3;
    private const int BUFFER_SAMPLES = 551; // ~25 ms @ 22.05 kHz
    private const int BUFFER_BYTES = BUFFER_SAMPLES * sizeof(short);
    private const int MAX_SFX_VOICES = 12;
    private const float BGM_RELATIVE_GAIN = 0.34f;
    private const uint WAVE_MAPPER = unchecked((uint)-1);
    private const uint CALLBACK_EVENT = 0x00050000u;
    private const uint WHDR_DONE = 0x00000001u;
    private const ushort WAVE_FORMAT_PCM = 1;

    private static readonly int s_waveHdrSize = Marshal.SizeOf<WAVEHDR>();
    private static readonly int s_dwFlagsOffset = (int)Marshal.OffsetOf<WAVEHDR>(nameof(WAVEHDR.dwFlags));

    private struct ActiveSfxVoice
    {
        public short[]? Samples;
        public int Position;
    }

    private readonly object _mixLock = new();
    private readonly ActiveSfxVoice[] _sfxVoices = new ActiveSfxVoice[MAX_SFX_VOICES];
    private readonly int[] _mixAccum = new int[BUFFER_SAMPLES];

    private readonly byte[][] _bufferData = new byte[BUFFER_COUNT][];
    private readonly GCHandle[] _bufferPins = new GCHandle[BUFFER_COUNT];
    private readonly IntPtr[] _headerPtrs = new IntPtr[BUFFER_COUNT];
    private readonly bool[] _headerPrepared = new bool[BUFFER_COUNT];

    private AutoResetEvent? _bufferDoneEvent;
    private Thread? _mixerThread;
    private IntPtr _waveOutHandle;
    private short[]? _bgmClip;
    private int _bgmPosition;
    private volatile bool _stopRequested;
    private volatile bool _isMuted;
    private volatile int _volume = 80;
    private bool _disposed;

    /// <summary>Gets whether a Win32 audio output device opened successfully.</summary>
    public bool IsDeviceAvailable => _waveOutHandle != IntPtr.Zero && !_disposed;

    /// <summary>Gets or sets whether audio output is muted live during playback.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    /// <summary>Gets or sets master volume percentage [0, 100] applied live during mixing.</summary>
    public int Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0, 100);
    }

    /// <summary>Initializes the <see cref="WaveOutAudioEngine"/> and starts the background mixer thread if audio hardware is present.</summary>
    public WaveOutAudioEngine()
    {
        InitializeDevice();
    }

    /// <summary>Starts or switches the looping background music track cleanly.</summary>
    public void PlayBgm(short[]? clip)
    {
        if (_disposed)
        {
            return;
        }

        lock (_mixLock)
        {
            if (ReferenceEquals(_bgmClip, clip))
            {
                return;
            }

            _bgmClip = clip;
            _bgmPosition = 0;
        }
    }

    /// <summary>Stops any currently looping background music track.</summary>
    public void StopBgm()
    {
        lock (_mixLock)
        {
            _bgmClip = null;
            _bgmPosition = 0;
        }
    }

    /// <summary>Queues a one-shot SFX clip onto the polyphonic mixer.</summary>
    public void EnqueueSfx(short[]? clip)
    {
        if (_disposed || clip is null || clip.Length == 0 || !IsDeviceAvailable)
        {
            return;
        }

        lock (_mixLock)
        {
            int targetIdx = -1;
            int mostAdvancedIdx = 0;
            double highestProgress = -1.0;

            for (int i = 0; i < MAX_SFX_VOICES; i++)
            {
                if (_sfxVoices[i].Samples is null)
                {
                    targetIdx = i;
                    break;
                }

                double progress = _sfxVoices[i].Position / (double)Math.Max(1, _sfxVoices[i].Samples!.Length);
                if (progress > highestProgress)
                {
                    highestProgress = progress;
                    mostAdvancedIdx = i;
                }
            }

            int idx = targetIdx >= 0 ? targetIdx : mostAdvancedIdx;
            _sfxVoices[idx].Samples = clip;
            _sfxVoices[idx].Position = 0;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopRequested = true;
        _bufferDoneEvent?.Set();

        if (_mixerThread is not null && _mixerThread.IsAlive)
        {
            _mixerThread.Join(600);
        }

        ReleaseNativeResources();
    }

    private void InitializeDevice()
    {
        _bufferDoneEvent = new AutoResetEvent(false);
        var format = new WAVEFORMATEX
        {
            wFormatTag = WAVE_FORMAT_PCM,
            nChannels = 1,
            nSamplesPerSec = ProceduralSoundBank.SAMPLE_RATE,
            wBitsPerSample = 16,
            nBlockAlign = sizeof(short),
            nAvgBytesPerSec = ProceduralSoundBank.SAMPLE_RATE * sizeof(short),
            cbSize = 0
        };

        try
        {
            uint result = waveOutOpen(
                out _waveOutHandle,
                WAVE_MAPPER,
                ref format,
                _bufferDoneEvent.SafeWaitHandle.DangerousGetHandle(),
                IntPtr.Zero,
                CALLBACK_EVENT);

            if (result != 0 || _waveOutHandle == IntPtr.Zero)
            {
                _waveOutHandle = IntPtr.Zero;
                ReleaseNativeResources();
                return;
            }

            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                _bufferData[i] = new byte[BUFFER_BYTES];
                _bufferPins[i] = GCHandle.Alloc(_bufferData[i], GCHandleType.Pinned);
                IntPtr hdrPtr = Marshal.AllocHGlobal(s_waveHdrSize);
                _headerPtrs[i] = hdrPtr;

                var hdr = new WAVEHDR
                {
                    lpData = _bufferPins[i].AddrOfPinnedObject(),
                    dwBufferLength = (uint)BUFFER_BYTES,
                    dwFlags = 0,
                    dwLoops = 0
                };
                Marshal.StructureToPtr(hdr, hdrPtr, fDeleteOld: false);

                if (waveOutPrepareHeader(_waveOutHandle, hdrPtr, (uint)s_waveHdrSize) != 0)
                {
                    ReleaseNativeResources();
                    return;
                }

                _headerPrepared[i] = true;
                FillMixedBuffer(_bufferData[i]);
                waveOutWrite(_waveOutHandle, hdrPtr, (uint)s_waveHdrSize);
            }

            _mixerThread = new Thread(MixerLoop)
            {
                IsBackground = true,
                Name = "MiniNecesse-AudioMixer",
                Priority = ThreadPriority.AboveNormal
            };
            _mixerThread.Start();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException)
        {
            _waveOutHandle = IntPtr.Zero;
            ReleaseNativeResources();
        }
    }

    private void MixerLoop()
    {
        WaitHandle waitEvent = _bufferDoneEvent!;
        while (!_stopRequested)
        {
            waitEvent.WaitOne(35);
            if (_stopRequested || _waveOutHandle == IntPtr.Zero)
            {
                break;
            }

            for (int i = 0; i < BUFFER_COUNT && !_stopRequested; i++)
            {
                IntPtr hdrPtr = _headerPtrs[i];
                if (hdrPtr == IntPtr.Zero)
                {
                    continue;
                }

                uint flags = (uint)Marshal.ReadInt32(hdrPtr, s_dwFlagsOffset);
                if ((flags & WHDR_DONE) == 0)
                {
                    continue;
                }

                Marshal.WriteInt32(hdrPtr, s_dwFlagsOffset, (int)(flags & ~WHDR_DONE));
                FillMixedBuffer(_bufferData[i]);
                waveOutWrite(_waveOutHandle, hdrPtr, (uint)s_waveHdrSize);
            }
        }
    }

    private void FillMixedBuffer(byte[] destinationBytes)
    {
        Array.Clear(_mixAccum, 0, BUFFER_SAMPLES);
        float masterGain = _isMuted ? 0f : _volume / 100f;

        lock (_mixLock)
        {
            if (masterGain > 0.001f)
            {
                if (_bgmClip is { Length: > 0 } bgm)
                {
                    float bgmScale = BGM_RELATIVE_GAIN * masterGain;
                    int pos = _bgmPosition;
                    for (int i = 0; i < BUFFER_SAMPLES; i++)
                    {
                        _mixAccum[i] += (int)(bgm[pos] * bgmScale);
                        pos++;
                        if (pos >= bgm.Length)
                        {
                            pos = 0;
                        }
                    }

                    _bgmPosition = pos;
                }

                for (int v = 0; v < MAX_SFX_VOICES; v++)
                {
                    short[]? sfx = _sfxVoices[v].Samples;
                    if (sfx is null)
                    {
                        continue;
                    }

                    int pos = _sfxVoices[v].Position;
                    int remaining = sfx.Length - pos;
                    int copyCount = Math.Min(BUFFER_SAMPLES, remaining);
                    for (int i = 0; i < copyCount; i++)
                    {
                        _mixAccum[i] += (int)(sfx[pos + i] * masterGain);
                    }

                    pos += copyCount;
                    if (pos >= sfx.Length)
                    {
                        _sfxVoices[v].Samples = null;
                        _sfxVoices[v].Position = 0;
                    }
                    else
                    {
                        _sfxVoices[v].Position = pos;
                    }
                }
            }
            else
            {
                // Keep BGM/SFX clocks advancing cleanly while muted so unmuting resumes smoothly.
                if (_bgmClip is { Length: > 0 } bgm)
                {
                    _bgmPosition = (_bgmPosition + BUFFER_SAMPLES) % bgm.Length;
                }

                for (int v = 0; v < MAX_SFX_VOICES; v++)
                {
                    short[]? sfx = _sfxVoices[v].Samples;
                    if (sfx is null) continue;
                    int nextPos = _sfxVoices[v].Position + BUFFER_SAMPLES;
                    if (nextPos >= sfx.Length)
                    {
                        _sfxVoices[v].Samples = null;
                        _sfxVoices[v].Position = 0;
                    }
                    else
                    {
                        _sfxVoices[v].Position = nextPos;
                    }
                }
            }
        }

        for (int i = 0; i < BUFFER_SAMPLES; i++)
        {
            int mixed = _mixAccum[i];
            if (mixed > short.MaxValue) mixed = short.MaxValue;
            else if (mixed < short.MinValue) mixed = short.MinValue;

            short s = (short)mixed;
            int byteIdx = i * 2;
            destinationBytes[byteIdx] = (byte)(s & 0xFF);
            destinationBytes[byteIdx + 1] = (byte)((s >> 8) & 0xFF);
        }
    }

    private void ReleaseNativeResources()
    {
        if (_waveOutHandle != IntPtr.Zero)
        {
            waveOutReset(_waveOutHandle);
            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                if (_headerPrepared[i] && _headerPtrs[i] != IntPtr.Zero)
                {
                    waveOutUnprepareHeader(_waveOutHandle, _headerPtrs[i], (uint)s_waveHdrSize);
                    _headerPrepared[i] = false;
                }
            }

            waveOutClose(_waveOutHandle);
            _waveOutHandle = IntPtr.Zero;
        }

        for (int i = 0; i < BUFFER_COUNT; i++)
        {
            if (_headerPtrs[i] != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_headerPtrs[i]);
                _headerPtrs[i] = IntPtr.Zero;
            }

            if (_bufferPins[i].IsAllocated)
            {
                _bufferPins[i].Free();
            }
        }

        _bufferDoneEvent?.Dispose();
        _bufferDoneEvent = null;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern uint waveOutOpen(
        out IntPtr phwo,
        uint uDeviceID,
        ref WAVEFORMATEX pwfx,
        IntPtr dwCallback,
        IntPtr dwInstance,
        uint fdwOpen);

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern uint waveOutPrepareHeader(IntPtr hwo, IntPtr pwh, uint cbwh);

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern uint waveOutWrite(IntPtr hwo, IntPtr pwh, uint cbwh);

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern uint waveOutUnprepareHeader(IntPtr hwo, IntPtr pwh, uint cbwh);

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern uint waveOutReset(IntPtr hwo);

    [DllImport("winmm.dll", SetLastError = false)]
    private static extern uint waveOutClose(IntPtr hwo);
}
