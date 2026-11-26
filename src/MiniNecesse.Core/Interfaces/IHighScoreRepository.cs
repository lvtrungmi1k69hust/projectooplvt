using System.Collections.Generic;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Persistence abstraction (DIP) for reading and updating Top 10 high-score leaderboards
/// isolated per (<see cref="GameMode"/> × <see cref="Difficulty"/>) bucket.
/// </summary>
public interface IHighScoreRepository
{
    /// <summary>Loads the Top 10 high-score entries overall, ordered descending by score. Returns an empty list if none exist or file is unreadable.</summary>
    IReadOnlyList<HighScoreEntry> LoadTopScores();

    /// <summary>Loads the Top 10 high-score entries strictly matching (<paramref name="mode"/>, <paramref name="difficulty"/>).</summary>
    IReadOnlyList<HighScoreEntry> LoadTopScores(GameMode mode, Difficulty difficulty);

    /// <summary>Records a new score entry, trims its (Mode, Difficulty) bucket to Top 10, and persists safely.</summary>
    void RecordScore(HighScoreEntry entry);

    /// <summary>Clears all persisted high-score records.</summary>
    void Clear();
}
