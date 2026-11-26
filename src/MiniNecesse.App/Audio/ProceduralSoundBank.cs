using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MiniNecesse.App.Audio;

/// <summary>
/// Provides 22.05 kHz 16-bit mono PCM audio clips for looping map/boss background music
/// and distinct tool/combat/consumption SFX cues. Checks optional user-supplied WAV files
/// in <c>Assets/Audio/*.wav</c> before generating clean procedural PCM clips in memory.
/// </summary>
public sealed class ProceduralSoundBank
{
    /// <summary>Output sample rate shared with <see cref="WaveOutAudioEngine"/>.</summary>
    public const int SAMPLE_RATE = 22050;

    private const int LOOP_FADE_SAMPLES = 360;

    private readonly string _audioDirectory;
    private readonly Dictionary<string, short[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="ProceduralSoundBank"/> class.</summary>
    public ProceduralSoundBank(string audioDirectory)
    {
        _audioDirectory = audioDirectory ?? string.Empty;
    }

    /// <summary>Gets (and caches) mono 16-bit PCM samples for <paramref name="cueKey"/>.</summary>
    public short[]? GetClip(string cueKey)
    {
        if (string.IsNullOrWhiteSpace(cueKey))
        {
            return null;
        }

        if (_cache.TryGetValue(cueKey, out short[]? cached))
        {
            return cached;
        }

        short[]? clip = TryLoadWavOverride(cueKey) ?? SynthesizeClip(cueKey);
        if (clip is not null)
        {
            _cache[cueKey] = clip;
        }

        return clip;
    }

    private short[]? TryLoadWavOverride(string cueKey)
    {
        if (string.IsNullOrWhiteSpace(_audioDirectory))
        {
            return null;
        }

        string wavPath = Path.Combine(_audioDirectory, $"{cueKey}.wav");
        if (!File.Exists(wavPath))
        {
            return null;
        }

        try
        {
            using var fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.ASCII);
            if (fs.Length < 44)
            {
                return null;
            }

            string riff = new(br.ReadChars(4));
            br.ReadInt32();
            string wave = new(br.ReadChars(4));
            if (!string.Equals(riff, "RIFF", StringComparison.Ordinal) ||
                !string.Equals(wave, "WAVE", StringComparison.Ordinal))
            {
                return null;
            }

            ushort audioFormat = 0;
            ushort channels = 0;
            int sourceRate = 0;
            ushort bitsPerSample = 0;
            byte[]? rawData = null;

            while (fs.Position + 8 <= fs.Length)
            {
                string chunkId = new(br.ReadChars(4));
                int chunkSize = br.ReadInt32();
                if (chunkSize < 0 || fs.Position + chunkSize > fs.Length)
                {
                    break;
                }

                long chunkEnd = fs.Position + chunkSize + (chunkSize & 1);
                if (string.Equals(chunkId, "fmt ", StringComparison.Ordinal) && chunkSize >= 16)
                {
                    audioFormat = br.ReadUInt16();
                    channels = br.ReadUInt16();
                    sourceRate = br.ReadInt32();
                    br.ReadInt32();
                    br.ReadUInt16();
                    bitsPerSample = br.ReadUInt16();
                }
                else if (string.Equals(chunkId, "data", StringComparison.Ordinal) && chunkSize > 0)
                {
                    rawData = br.ReadBytes(chunkSize);
                }

                fs.Position = Math.Min(fs.Length, chunkEnd);
            }

            if (rawData is null || audioFormat != 1 || bitsPerSample != 16 || (channels != 1 && channels != 2) || sourceRate <= 0)
            {
                return null;
            }

            int bytesPerFrame = channels * 2;
            int frameCount = rawData.Length / bytesPerFrame;
            if (frameCount <= 0)
            {
                return null;
            }

            var mono = new short[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                int offset = i * bytesPerFrame;
                if (channels == 1)
                {
                    mono[i] = BitConverter.ToInt16(rawData, offset);
                }
                else
                {
                    int left = BitConverter.ToInt16(rawData, offset);
                    int right = BitConverter.ToInt16(rawData, offset + 2);
                    mono[i] = (short)((left + right) / 2);
                }
            }

            return sourceRate == SAMPLE_RATE ? mono : ResampleMono(mono, sourceRate, SAMPLE_RATE);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            return null;
        }
    }

    private static short[] ResampleMono(short[] source, int sourceRate, int targetRate)
    {
        double ratio = (double)sourceRate / targetRate;
        int outLen = Math.Max(1, (int)Math.Round(source.Length / ratio));
        var output = new short[outLen];
        for (int i = 0; i < outLen; i++)
        {
            double pos = i * ratio;
            int idx0 = (int)pos;
            int idx1 = Math.Min(source.Length - 1, idx0 + 1);
            double frac = pos - idx0;
            double value = source[idx0] * (1.0 - frac) + source[idx1] * frac;
            output[i] = ClampSample(value);
        }

        return output;
    }

    private static short[]? SynthesizeClip(string cueKey)
    {
        return cueKey.ToLowerInvariant() switch
        {
            "bgm_day" => SynthesizeDayBgm(),
            "bgm_night" => SynthesizeNightBgm(),
            "bgm_boss" => SynthesizeBossBgm(),
            "tool_axe" => SynthesizeAxeChop(),
            "tool_pickaxe" => SynthesizePickaxeClang(),
            "tool_hoe" => SynthesizeHoeTill(),
            "tool_sword" => SynthesizeSwordSlash(),
            "tool_bow" => SynthesizeBowRelease(),
            "swing" => SynthesizeLightSwing(),
            "eat" => SynthesizeEatCrunch(),
            "potion" => SynthesizePotionChime(),
            "armor_equip" => SynthesizeArmorEquip(),
            "hit" => SynthesizeImpactHit(),
            "player_hurt" => SynthesizePlayerHurt(),
            "shoot" => SynthesizeProjectileSnap(),
            "harvest" => SynthesizeHarvestPop(),
            "build" => SynthesizeBuildPlace(),
            "craft" => SynthesizeCraftAnvil(),
            "pickup" => SynthesizePickupSparkle(),
            "raid_horn" => SynthesizeRaidHorn(),
            "enemy_defeated" => SynthesizeEnemyDefeated(),
            "boss_spawn" => SynthesizeBossSpawn(),
            "boss_enrage" => SynthesizeBossEnrage(),
            "boss_defeated" => SynthesizeBossDefeated(),
            "structure_break" => SynthesizeStructureBreak(),
            "victory" => SynthesizeVictoryFanfare(),
            "game_over" => SynthesizeGameOver(),
            _ => SynthesizeLightSwing()
        };
    }

    private static short[] SynthesizeDayBgm()
    {
        const double durationSec = 12.0; // 16 quarter beats @ 80 BPM
        int totalSamples = (int)(SAMPLE_RATE * durationSec);
        var buf = new double[totalSamples];

        // 4 bars: Cmaj -> G -> Am -> F (gentle adventure theme)
        double[] bassRoots = { 130.81, 98.00, 110.00, 87.31 };
        double[] fifthRoots = { 196.00, 146.83, 164.81, 130.81 };
        double[] melodyNotes =
        {
            523.25, 659.25, 587.33, 523.25,  659.25, 783.99, 659.25, 0,
            587.33, 493.88, 587.33, 698.46,  659.25, 587.33, 523.25, 0,
            440.00, 523.25, 659.25, 587.33,  523.25, 440.00, 392.00, 0,
            349.23, 440.00, 523.25, 587.33,  659.25, 523.25, 587.33, 523.25
        };

        const double barDuration = 3.0;
        const double eighthDuration = durationSec / 32.0;

        for (int i = 0; i < totalSamples; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            int barIdx = Math.Min(3, (int)(t / barDuration));
            double barT = t - (barIdx * barDuration);
            double chordPad =
                0.09 * Math.Sin(2.0 * Math.PI * bassRoots[barIdx] * t) +
                0.06 * Math.Sin(2.0 * Math.PI * fifthRoots[barIdx] * t);

            // Soft beat pulse
            double beatPos = (barT % 0.75) / 0.75;
            double bassEnv = Math.Exp(-beatPos * 3.8);
            double bass = 0.14 * bassEnv * TriangleWave(2.0 * Math.PI * bassRoots[barIdx] * t);

            int stepIdx = Math.Min(melodyNotes.Length - 1, (int)(t / eighthDuration));
            double stepT = (t - (stepIdx * eighthDuration)) / eighthDuration;
            double noteFreq = melodyNotes[stepIdx];
            double melody = 0.0;
            if (noteFreq > 0)
            {
                double env = Math.Min(1.0, stepT * 14.0) * Math.Exp(-stepT * 2.6);
                melody = 0.19 * env * (0.75 * Math.Sin(2.0 * Math.PI * noteFreq * t) + 0.25 * Math.Sin(4.0 * Math.PI * noteFreq * t));
            }

            buf[i] = chordPad + bass + melody;
        }

        ApplySeamlessLoopTaper(buf);
        return ConvertToPcm16(buf, 0.72);
    }

    private static short[] SynthesizeNightBgm()
    {
        const double durationSec = 12.0;
        int totalSamples = (int)(SAMPLE_RATE * durationSec);
        var buf = new double[totalSamples];

        // D minor -> Bb -> C -> Am nocturnal ambience
        double[] droneRoots = { 146.83, 116.54, 130.81, 110.00 };
        double[] arpNotes =
        {
            440.00, 523.25, 587.33, 0, 523.25, 440.00, 0, 392.00,
            349.23, 440.00, 587.33, 0, 523.25, 440.00, 0, 0,
            392.00, 523.25, 659.25, 0, 587.33, 493.88, 0, 392.00,
            440.00, 523.25, 659.25, 587.33, 523.25, 440.00, 329.63, 0
        };

        const double eighthDuration = durationSec / 32.0;
        for (int i = 0; i < totalSamples; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            int barIdx = Math.Min(3, (int)(t / 3.0));
            double pad =
                0.11 * Math.Sin(2.0 * Math.PI * droneRoots[barIdx] * t) +
                0.06 * Math.Sin(2.0 * Math.PI * (droneRoots[barIdx] * 1.498) * t) * (0.65 + 0.35 * Math.Sin(2.0 * Math.PI * 0.25 * t));

            int stepIdx = Math.Min(arpNotes.Length - 1, (int)(t / eighthDuration));
            double stepT = (t - (stepIdx * eighthDuration)) / eighthDuration;
            double noteFreq = arpNotes[stepIdx];
            double bell = 0.0;
            if (noteFreq > 0)
            {
                double env = Math.Min(1.0, stepT * 24.0) * Math.Exp(-stepT * 3.8);
                bell = 0.16 * env * (Math.Sin(2.0 * Math.PI * noteFreq * t) + 0.32 * Math.Sin(6.0 * Math.PI * noteFreq * t));
            }

            buf[i] = pad + bell;
        }

        ApplySeamlessLoopTaper(buf);
        return ConvertToPcm16(buf, 0.68);
    }

    private static short[] SynthesizeBossBgm()
    {
        const double durationSec = 8.0; // 16 beats @ 120 BPM
        int totalSamples = (int)(SAMPLE_RATE * durationSec);
        var buf = new double[totalSamples];

        // Driving E minor ostinato + percussion + heroic threat lead
        double[] bassPattern =
        {
            82.41, 82.41, 164.81, 82.41, 98.00, 82.41, 110.00, 123.47,
            82.41, 82.41, 164.81, 82.41, 130.81, 123.47, 110.00, 98.00
        };
        double[] leadPattern =
        {
            659.25, 0, 659.25, 783.99, 739.99, 659.25, 587.33, 659.25,
            987.77, 880.00, 783.99, 739.99, 659.25, 587.33, 493.88, 587.33
        };

        const double stepSec = durationSec / 32.0;
        const double leadSec = durationSec / 16.0;
        uint rng = 0x9E3779B9u;

        for (int i = 0; i < totalSamples; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            int stepIdx = Math.Min(31, (int)(t / stepSec)) % bassPattern.Length;
            double bassPhase = (t - ((int)(t / stepSec) * stepSec)) / stepSec;
            double bassFreq = bassPattern[stepIdx];
            double bassEnv = Math.Exp(-bassPhase * 3.2);
            double bass = 0.23 * bassEnv * SoftSawWave(2.0 * Math.PI * bassFreq * t);

            int leadIdx = Math.Min(15, (int)(t / leadSec));
            double leadPhase = (t - (leadIdx * leadSec)) / leadSec;
            double leadFreq = leadPattern[leadIdx];
            double lead = 0.0;
            if (leadFreq > 0)
            {
                double leadEnv = Math.Min(1.0, leadPhase * 16.0) * Math.Exp(-leadPhase * 1.9);
                lead = 0.17 * leadEnv * (0.65 * SoftSquareWave(2.0 * Math.PI * leadFreq * t) + 0.35 * Math.Sin(4.0 * Math.PI * leadFreq * t));
            }

            // Kick on quarter beats + crisp hi-hat offbeat
            double beatT = t % 0.5;
            double kick = beatT < 0.09 ? 0.24 * Math.Exp(-beatT * 36.0) * Math.Sin(2.0 * Math.PI * (125.0 - 750.0 * beatT) * beatT) : 0.0;
            double hatT = (t + 0.25) % 0.5;
            double hat = hatT < 0.035 ? 0.07 * Math.Exp(-hatT * 95.0) * NextWhiteNoise(ref rng) : 0.0;

            buf[i] = bass + lead + kick + hat;
        }

        ApplySeamlessLoopTaper(buf);
        return ConvertToPcm16(buf, 0.80);
    }

    private static short[] SynthesizeAxeChop()
    {
        int n = (int)(SAMPLE_RATE * 0.15);
        var buf = new double[n];
        uint rng = 0xA1E1101u;
        double lp = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double freq = 230.0 - 150.0 * Math.Min(1.0, t / 0.07);
            double woodBody = Math.Sin(2.0 * Math.PI * freq * t) * Math.Exp(-t * 28.0);
            double splinterRaw = NextWhiteNoise(ref rng);
            lp = lp * 0.58 + splinterRaw * 0.42;
            double splinter = lp * Math.Exp(-t * 42.0) * 0.55;
            buf[i] = woodBody * 0.72 + splinter;
        }

        return ConvertToPcm16(buf, 0.90);
    }

    private static short[] SynthesizePickaxeClang()
    {
        int n = (int)(SAMPLE_RATE * 0.19);
        var buf = new double[n];
        uint rng = 0x51CC3A2Eu;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double clang =
                0.42 * Math.Sin(2.0 * Math.PI * 1440.0 * t) * Math.Exp(-t * 22.0) +
                0.30 * Math.Sin(2.0 * Math.PI * 2210.0 * t) * Math.Exp(-t * 30.0) +
                0.20 * Math.Sin(2.0 * Math.PI * 3190.0 * t) * Math.Exp(-t * 40.0);
            double rockCrack = NextWhiteNoise(ref rng) * Math.Exp(-t * 65.0) * 0.35;
            buf[i] = clang + rockCrack;
        }

        return ConvertToPcm16(buf, 0.88);
    }

    private static short[] SynthesizeHoeTill()
    {
        int n = (int)(SAMPLE_RATE * 0.15);
        var buf = new double[n];
        uint rng = 0x70E123u;
        double lp = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            lp = lp * 0.82 + NextWhiteNoise(ref rng) * 0.18;
            double soil = lp * Math.Exp(-t * 24.0) * 0.85;
            double thump = Math.Sin(2.0 * Math.PI * (140.0 - 65.0 * (t / 0.15)) * t) * Math.Exp(-t * 26.0) * 0.55;
            buf[i] = soil + thump;
        }

        return ConvertToPcm16(buf, 0.90);
    }

    private static short[] SynthesizeSwordSlash()
    {
        int n = (int)(SAMPLE_RATE * 0.17);
        var buf = new double[n];
        uint rng = 0x50D57A5u;
        double prev = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double raw = NextWhiteNoise(ref rng);
            double hp = raw - prev * 0.82;
            prev = raw;
            double whooshEnv = Math.Sin(Math.PI * Math.Min(1.0, t / 0.13)) * Math.Exp(-t * 14.0);
            double bladeShing = Math.Sin(2.0 * Math.PI * (1820.0 - 480.0 * (t / 0.17)) * t) * Math.Exp(-t * 24.0) * 0.28;
            buf[i] = hp * whooshEnv * 0.60 + bladeShing;
        }

        return ConvertToPcm16(buf, 0.88);
    }

    private static short[] SynthesizeBowRelease()
    {
        int n = (int)(SAMPLE_RATE * 0.18);
        var buf = new double[n];
        uint rng = 0xB0733Au;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double stringFreq = 260.0 - 70.0 * Math.Min(1.0, t / 0.14);
            double twang = (Math.Sin(2.0 * Math.PI * stringFreq * t) + 0.4 * Math.Sin(4.0 * Math.PI * stringFreq * t)) * Math.Exp(-t * 22.0) * 0.55;
            double arrowZip = NextWhiteNoise(ref rng) * Math.Sin(Math.PI * Math.Min(1.0, t / 0.11)) * Math.Exp(-t * 20.0) * 0.35;
            buf[i] = twang + arrowZip;
        }

        return ConvertToPcm16(buf, 0.88);
    }

    private static short[] SynthesizeLightSwing()
    {
        int n = (int)(SAMPLE_RATE * 0.12);
        var buf = new double[n];
        uint rng = 0x3311AAu;
        double prev = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double raw = NextWhiteNoise(ref rng);
            double filtered = raw * 0.5 + prev * 0.3;
            prev = raw;
            buf[i] = filtered * Math.Sin(Math.PI * (i / (double)n)) * Math.Exp(-t * 15.0) * 0.55;
        }

        return ConvertToPcm16(buf, 0.78);
    }

    private static short[] SynthesizeEatCrunch()
    {
        int n = (int)(SAMPLE_RATE * 0.30);
        var buf = new double[n];
        uint rng = 0xEA7F00Du;
        double[] biteStarts = { 0.0, 0.095, 0.185 };
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double sample = 0.0;
            for (int b = 0; b < biteStarts.Length; b++)
            {
                double bt = t - biteStarts[b];
                if (bt >= 0 && bt < 0.075)
                {
                    double env = Math.Exp(-bt * 42.0);
                    sample += (NextWhiteNoise(ref rng) * 0.55 + Math.Sin(2.0 * Math.PI * 190.0 * bt) * 0.35) * env;
                }
            }

            buf[i] = sample;
        }

        return ConvertToPcm16(buf, 0.88);
    }

    private static short[] SynthesizePotionChime()
    {
        return SynthesizeArpeggio(new[] { 523.25, 659.25, 783.99, 1046.50 }, 0.058, 0.075, 0.78);
    }

    private static short[] SynthesizeArmorEquip()
    {
        int n = (int)(SAMPLE_RATE * 0.18);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double clack1 = Math.Sin(2.0 * Math.PI * 760.0 * t) * Math.Exp(-t * 36.0) * 0.45;
            double t2 = t - 0.065;
            double clack2 = t2 >= 0 ? Math.Sin(2.0 * Math.PI * 1120.0 * t2) * Math.Exp(-t2 * 30.0) * 0.55 : 0.0;
            buf[i] = clack1 + clack2;
        }

        return ConvertToPcm16(buf, 0.85);
    }

    private static short[] SynthesizeImpactHit()
    {
        int n = (int)(SAMPLE_RATE * 0.12);
        var buf = new double[n];
        uint rng = 0xB17B17u;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double body = Math.Sin(2.0 * Math.PI * (190.0 - 120.0 * (t / 0.12)) * t) * Math.Exp(-t * 32.0);
            double snap = NextWhiteNoise(ref rng) * Math.Exp(-t * 58.0) * 0.32;
            buf[i] = body * 0.72 + snap;
        }

        return ConvertToPcm16(buf, 0.85);
    }

    private static short[] SynthesizePlayerHurt()
    {
        int n = (int)(SAMPLE_RATE * 0.19);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double freq = 220.0 - 115.0 * (t / 0.19);
            double tone = (0.7 * Math.Sin(2.0 * Math.PI * freq * t) + 0.3 * SoftSquareWave(2.0 * Math.PI * freq * 0.5 * t)) * Math.Exp(-t * 18.0);
            buf[i] = tone;
        }

        return ConvertToPcm16(buf, 0.86);
    }

    private static short[] SynthesizeProjectileSnap()
    {
        int n = (int)(SAMPLE_RATE * 0.13);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double freq = 620.0 - 380.0 * (t / 0.13);
            buf[i] = Math.Sin(2.0 * Math.PI * freq * t) * Math.Exp(-t * 28.0) * 0.65;
        }

        return ConvertToPcm16(buf, 0.75);
    }

    private static short[] SynthesizeHarvestPop()
    {
        return SynthesizeArpeggio(new[] { 440.00, 659.25, 880.00 }, 0.045, 0.070, 0.76);
    }

    private static short[] SynthesizeBuildPlace()
    {
        int n = (int)(SAMPLE_RATE * 0.14);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double knock = Math.Sin(2.0 * Math.PI * (240.0 - 110.0 * (t / 0.14)) * t) * Math.Exp(-t * 30.0);
            double click = Math.Sin(2.0 * Math.PI * 880.0 * t) * Math.Exp(-t * 75.0) * 0.30;
            buf[i] = knock * 0.72 + click;
        }

        return ConvertToPcm16(buf, 0.82);
    }

    private static short[] SynthesizeCraftAnvil()
    {
        int n = (int)(SAMPLE_RATE * 0.25);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double hit1 = (Math.Sin(2.0 * Math.PI * 980.0 * t) + 0.35 * Math.Sin(2.0 * Math.PI * 1960.0 * t)) * Math.Exp(-t * 24.0) * 0.45;
            double t2 = t - 0.085;
            double hit2 = t2 >= 0 ? (Math.Sin(2.0 * Math.PI * 1240.0 * t2) + 0.35 * Math.Sin(2.0 * Math.PI * 2480.0 * t2)) * Math.Exp(-t2 * 22.0) * 0.55 : 0.0;
            buf[i] = hit1 + hit2;
        }

        return ConvertToPcm16(buf, 0.82);
    }

    private static short[] SynthesizePickupSparkle()
    {
        return SynthesizeArpeggio(new[] { 783.99, 1174.66 }, 0.045, 0.075, 0.72);
    }

    private static short[] SynthesizeRaidHorn()
    {
        int n = (int)(SAMPLE_RATE * 0.56);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double env = Math.Min(1.0, t / 0.10) * Math.Exp(-Math.Max(0.0, t - 0.18) * 3.8);
            double horn =
                0.55 * SoftSawWave(2.0 * Math.PI * 146.83 * t) +
                0.35 * SoftSawWave(2.0 * Math.PI * 220.00 * t);
            buf[i] = horn * env * 0.70;
        }

        return ConvertToPcm16(buf, 0.80);
    }

    private static short[] SynthesizeEnemyDefeated()
    {
        int n = (int)(SAMPLE_RATE * 0.20);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double pop = Math.Sin(2.0 * Math.PI * (420.0 - 260.0 * (t / 0.20)) * t) * Math.Exp(-t * 18.0) * 0.55;
            double t2 = t - 0.06;
            double ding = t2 >= 0 ? Math.Sin(2.0 * Math.PI * 660.0 * t2) * Math.Exp(-t2 * 26.0) * 0.45 : 0.0;
            buf[i] = pop + ding;
        }

        return ConvertToPcm16(buf, 0.78);
    }

    private static short[] SynthesizeBossSpawn()
    {
        int n = (int)(SAMPLE_RATE * 0.72);
        var buf = new double[n];
        uint rng = 0xB0551u;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double boom = Math.Sin(2.0 * Math.PI * (110.0 - 68.0 * Math.Min(1.0, t / 0.45)) * t) * Math.Exp(-t * 4.5) * 0.65;
            double brass = (SoftSawWave(2.0 * Math.PI * 164.81 * t) * 0.40 + SoftSawWave(2.0 * Math.PI * 246.94 * t) * 0.30) * Math.Min(1.0, t / 0.12) * Math.Exp(-t * 2.5);
            double grit = NextWhiteNoise(ref rng) * Math.Exp(-t * 12.0) * 0.22;
            buf[i] = boom + brass + grit;
        }

        return ConvertToPcm16(buf, 0.90);
    }

    private static short[] SynthesizeBossEnrage()
    {
        int n = (int)(SAMPLE_RATE * 0.48);
        var buf = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            double pitch = 180.0 + 250.0 * Math.Sin(Math.PI * Math.Min(1.0, t / 0.42));
            double growl = SoftSawWave(2.0 * Math.PI * pitch * t + 1.8 * Math.Sin(2.0 * Math.PI * 38.0 * t));
            double env = Math.Min(1.0, t / 0.06) * Math.Exp(-Math.Max(0.0, t - 0.18) * 5.0);
            buf[i] = growl * env * 0.70;
        }

        return ConvertToPcm16(buf, 0.85);
    }

    private static short[] SynthesizeBossDefeated()
    {
        return SynthesizeArpeggio(new[] { 261.63, 329.63, 392.00, 523.25, 659.25 }, 0.085, 0.16, 0.86);
    }

    private static short[] SynthesizeStructureBreak()
    {
        int n = (int)(SAMPLE_RATE * 0.26);
        var buf = new double[n];
        uint rng = 0xB8EA6u;
        double lp = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SAMPLE_RATE;
            lp = lp * 0.72 + NextWhiteNoise(ref rng) * 0.28;
            double crumble = lp * Math.Exp(-t * 14.0) * 0.75;
            double thud = Math.Sin(2.0 * Math.PI * (135.0 - 75.0 * (t / 0.26)) * t) * Math.Exp(-t * 18.0) * 0.55;
            buf[i] = crumble + thud;
        }

        return ConvertToPcm16(buf, 0.86);
    }

    private static short[] SynthesizeVictoryFanfare()
    {
        return SynthesizeArpeggio(new[] { 523.25, 659.25, 783.99, 1046.50 }, 0.11, 0.22, 0.85);
    }

    private static short[] SynthesizeGameOver()
    {
        return SynthesizeArpeggio(new[] { 329.63, 293.66, 261.63, 220.00 }, 0.13, 0.24, 0.80);
    }

    private static short[] SynthesizeArpeggio(double[] frequencies, double stepSec, double tailSec, double gain)
    {
        double totalSec = (frequencies.Length * stepSec) + tailSec;
        int n = (int)(SAMPLE_RATE * totalSec);
        var buf = new double[n];
        for (int s = 0; s < frequencies.Length; s++)
        {
            double start = s * stepSec;
            double freq = frequencies[s];
            int startIdx = (int)(start * SAMPLE_RATE);
            for (int i = startIdx; i < n; i++)
            {
                double dt = (i - startIdx) / (double)SAMPLE_RATE;
                double env = Math.Min(1.0, dt * 80.0) * Math.Exp(-dt * 10.5);
                buf[i] += (0.75 * Math.Sin(2.0 * Math.PI * freq * dt) + 0.25 * Math.Sin(4.0 * Math.PI * freq * dt)) * env * 0.45;
            }
        }

        return ConvertToPcm16(buf, gain);
    }

    private static void ApplySeamlessLoopTaper(double[] samples)
    {
        int taper = Math.Min(LOOP_FADE_SAMPLES, samples.Length / 4);
        for (int i = 0; i < taper; i++)
        {
            double k = i / (double)taper;
            samples[i] *= k;
            samples[samples.Length - 1 - i] *= k;
        }
    }

    private static short[] ConvertToPcm16(double[] floatSamples, double masterScale)
    {
        var output = new short[floatSamples.Length];
        for (int i = 0; i < floatSamples.Length; i++)
        {
            double clamped = Math.Clamp(floatSamples[i] * masterScale, -0.98, 0.98);
            output[i] = ClampSample(clamped * short.MaxValue);
        }

        return output;
    }

    private static short ClampSample(double value)
    {
        if (value >= short.MaxValue) return short.MaxValue;
        if (value <= short.MinValue) return short.MinValue;
        return (short)Math.Round(value);
    }

    private static double TriangleWave(double phaseRad)
    {
        double normalized = (phaseRad / (2.0 * Math.PI)) % 1.0;
        if (normalized < 0.0) normalized += 1.0;
        return 4.0 * Math.Abs(normalized - 0.5) - 1.0;
    }

    private static double SoftSawWave(double phaseRad)
    {
        return 0.65 * Math.Sin(phaseRad) + 0.24 * Math.Sin(2.0 * phaseRad) + 0.11 * Math.Sin(3.0 * phaseRad);
    }

    private static double SoftSquareWave(double phaseRad)
    {
        return 0.72 * Math.Sin(phaseRad) + 0.28 * Math.Sin(3.0 * phaseRad);
    }

    private static double NextWhiteNoise(ref uint state)
    {
        state = state * 1664525u + 1013904223u;
        return ((state >> 9) / (double)(1u << 23)) * 2.0 - 1.0;
    }
}
