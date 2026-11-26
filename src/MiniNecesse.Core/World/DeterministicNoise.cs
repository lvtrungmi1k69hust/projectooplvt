using System;

namespace MiniNecesse.Core.World;

/// <summary>
/// Shared deterministic bitwise hashing and 2D fractional Brownian motion (FBM) noise helpers
/// independent of process-randomized <see cref="HashCode"/> (DRY across Campaign and Endless generators).
/// </summary>
internal static class DeterministicNoise
{
    internal static float Fbm(float x, float y, int seed, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxAmplitude = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += amplitude * ValueNoise2D(x * frequency, y * frequency, seed + (i * 1009));
            maxAmplitude += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return maxAmplitude > 0f ? value / maxAmplitude : 0f;
    }

    internal static float ContrastStretch(float normalizedNoise, float gain = 2.35f)
    {
        float stretched = ((normalizedNoise - 0.5f) * gain) + 0.5f;
        return global::System.Math.Clamp(stretched, 0f, 1f);
    }

    internal static float ValueNoise2D(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x);
        int iy = (int)MathF.Floor(y);
        float fx = x - ix;
        float fy = y - iy;

        float u = fx * fx * (3f - (2f * fx));
        float v = fy * fy * (3f - (2f * fy));

        float n00 = HashToUnitFloat(ix, iy, seed);
        float n10 = HashToUnitFloat(ix + 1, iy, seed);
        float n01 = HashToUnitFloat(ix, iy + 1, seed);
        float n11 = HashToUnitFloat(ix + 1, iy + 1, seed);

        float nx0 = n00 + ((n10 - n00) * u);
        float nx1 = n01 + ((n11 - n01) * u);
        return nx0 + ((nx1 - nx0) * v);
    }

    internal static float HashToUnitFloat(int x, int y, int seed)
    {
        uint h = MixHash(x, y, seed);
        return (h & 0xFFFFu) / 65535f;
    }

    internal static uint MixHash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = ((uint)seed * 0x9E3779B9u) ^ 0xA5A5A5A5u;
            h ^= (uint)x * 0x85EBCA6Bu;
            h = (h ^ (h >> 16)) * 0xC2B2AE35u;
            h ^= (uint)y * 0x27D4EB2Fu;
            h ^= h >> 15;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            return h;
        }
    }
}
