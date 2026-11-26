using System;
using System.Collections.Generic;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Data-driven gameplay difficulty rule profile loaded from <c>difficulties.json</c>.
/// </summary>
public sealed record DifficultyProfile(
    Difficulty Difficulty,
    string DisplayName,
    string Description,
    float DamageTakenMultiplier,
    float EnemyDamageMultiplier,
    float EnemyHpMultiplier,
    float RaidIntensity,
    bool EnableTutorialGuide,
    bool PreventPlayerDeath);

/// <summary>
/// Registry mapping <see cref="Difficulty"/> presets to externalized <see cref="DifficultyProfile"/> rules.
/// </summary>
public sealed class DifficultyCatalog
{
    private readonly Dictionary<Difficulty, DifficultyProfile> _profiles = new();

    public void Register(DifficultyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profiles[profile.Difficulty] = profile;
    }

    public bool TryGetProfile(Difficulty difficulty, out DifficultyProfile? profile) =>
        _profiles.TryGetValue(difficulty, out profile);

    public DifficultyProfile GetProfile(Difficulty difficulty)
    {
        if (_profiles.TryGetValue(difficulty, out DifficultyProfile? profile))
        {
            return profile;
        }

        if (_profiles.TryGetValue(Difficulty.Normal, out DifficultyProfile? normal))
        {
            return normal;
        }

        return new DifficultyProfile(Difficulty.Normal, "Normal", "Standard balanced survival.", 1f, 1f, 1f, 1f, false, false);
    }

    public static DifficultyCatalog CreateDefault()
    {
        var catalog = new DifficultyCatalog();
        catalog.Register(new DifficultyProfile(
            Difficulty.Tutorial,
            "Tutorial",
            "Guided onboarding with 0 player damage taken.",
            DamageTakenMultiplier: 0f,
            EnemyDamageMultiplier: 1f,
            EnemyHpMultiplier: 0.75f,
            RaidIntensity: 0.75f,
            EnableTutorialGuide: true,
            PreventPlayerDeath: true));

        catalog.Register(new DifficultyProfile(
            Difficulty.Easy,
            "Easy",
            "Reduced enemy stats and gentler waves.",
            DamageTakenMultiplier: 0.85f,
            EnemyDamageMultiplier: 0.75f,
            EnemyHpMultiplier: 0.80f,
            RaidIntensity: 0.75f,
            EnableTutorialGuide: false,
            PreventPlayerDeath: false));

        catalog.Register(new DifficultyProfile(
            Difficulty.Normal,
            "Normal",
            "Standard balanced survival progression.",
            DamageTakenMultiplier: 1f,
            EnemyDamageMultiplier: 1f,
            EnemyHpMultiplier: 1f,
            RaidIntensity: 1f,
            EnableTutorialGuide: false,
            PreventPlayerDeath: false));

        catalog.Register(new DifficultyProfile(
            Difficulty.Hard,
            "Hard",
            "Amplified raids and heavy incoming damage.",
            DamageTakenMultiplier: 1.30f,
            EnemyDamageMultiplier: 1.35f,
            EnemyHpMultiplier: 1.35f,
            RaidIntensity: 1.50f,
            EnableTutorialGuide: false,
            PreventPlayerDeath: false));

        return catalog;
    }
}
