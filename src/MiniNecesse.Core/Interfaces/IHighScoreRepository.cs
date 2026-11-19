using System.Collections.Generic;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Persistence abstraction (DIP) for reading and updating the Top 10 high-score leaderboard.
/// </summary>
public interface IHighScoreRepository
{
    /// <summary>Loads the Top 10 high-score entries ordered descending by score. Returns an empty list if none exist.</summary>
    IReadOnlyList<HighScoreEntry> LoadTopScores();

    /// <summary>Records a new score entry, keeps the Top 10 highest entries, and persists them safely.</summary>
    void RecordScore(HighScoreEntry entry);

    /// <summary>Clears all persisted high-score records.</summary>
    void Clear();
}
