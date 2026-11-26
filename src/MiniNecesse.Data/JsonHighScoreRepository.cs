using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Data;

/// <summary>
/// Persists and loads Top 10 high-score leaderboards in <c>highscore.json</c> isolated per
/// (<see cref="GameMode"/> × <see cref="Difficulty"/>) bucket.
/// Safely backward-compatible with legacy schemas and crash-safe against corrupted files.
/// </summary>
public sealed class JsonHighScoreRepository : IHighScoreRepository
{
    private const int MAX_LEADERBOARD_ENTRIES_PER_BUCKET = 10;
    private const string DEFAULT_SAVES_FOLDER = "Saves";
    private const string HIGH_SCORE_FILE_NAME = "highscore.json";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    /// <summary>Gets the resolved path to <c>highscore.json</c>.</summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonHighScoreRepository"/> class.
    /// Defaults to relative <c>Saves/highscore.json</c> beside the executable.
    /// </summary>
    public JsonHighScoreRepository(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, DEFAULT_SAVES_FOLDER, HIGH_SCORE_FILE_NAME)
            : filePath;
    }

    /// <inheritdoc />
    public IReadOnlyList<HighScoreEntry> LoadTopScores()
    {
        List<HighScoreEntry> all = LoadAllNormalizedEntriesSafe();
        if (all.Count == 0)
        {
            return Array.Empty<HighScoreEntry>();
        }

        all.Sort(CompareEntries);
        if (all.Count > MAX_LEADERBOARD_ENTRIES_PER_BUCKET)
        {
            all.RemoveRange(MAX_LEADERBOARD_ENTRIES_PER_BUCKET, all.Count - MAX_LEADERBOARD_ENTRIES_PER_BUCKET);
        }

        return all.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<HighScoreEntry> LoadTopScores(GameMode mode, Difficulty difficulty)
    {
        string targetMode = mode.ToString();
        string targetDifficulty = difficulty.ToString();
        List<HighScoreEntry> all = LoadAllNormalizedEntriesSafe();
        if (all.Count == 0)
        {
            return Array.Empty<HighScoreEntry>();
        }

        var bucket = new List<HighScoreEntry>(MAX_LEADERBOARD_ENTRIES_PER_BUCKET);
        for (int i = 0; i < all.Count; i++)
        {
            HighScoreEntry e = all[i];
            if (string.Equals(e.Mode, targetMode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Difficulty, targetDifficulty, StringComparison.OrdinalIgnoreCase))
            {
                bucket.Add(e);
            }
        }

        bucket.Sort(CompareEntries);
        if (bucket.Count > MAX_LEADERBOARD_ENTRIES_PER_BUCKET)
        {
            bucket.RemoveRange(MAX_LEADERBOARD_ENTRIES_PER_BUCKET, bucket.Count - MAX_LEADERBOARD_ENTRIES_PER_BUCKET);
        }

        return bucket.AsReadOnly();
    }

    /// <inheritdoc />
    public void RecordScore(HighScoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);

        HighScoreEntry normalizedNew = NormalizeEntry(entry);
        List<HighScoreEntry> all = LoadAllNormalizedEntriesSafe();
        all.Add(normalizedNew);

        IReadOnlyList<HighScoreEntry> trimmedPerBucket = NormalizeAndTrimPerBucket(all);
        WriteEntriesAtomically(trimmedPerBucket);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private List<HighScoreEntry> LoadAllNormalizedEntriesSafe()
    {
        if (!File.Exists(_filePath))
        {
            return new List<HighScoreEntry>();
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<HighScoreEntry>();
            }

            List<HighScoreJsonRecord>? rawList = JsonSerializer.Deserialize<List<HighScoreJsonRecord>>(json, JSON_OPTIONS);
            if (rawList is null || rawList.Count == 0)
            {
                return new List<HighScoreEntry>();
            }

            var valid = new List<HighScoreEntry>(rawList.Count);
            for (int i = 0; i < rawList.Count; i++)
            {
                HighScoreJsonRecord raw = rawList[i];
                if (raw is null || string.IsNullOrWhiteSpace(raw.PlayerName) || raw.Score < 0)
                {
                    continue;
                }

                valid.Add(NormalizeFromRecord(raw));
            }

            return valid;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Debug.WriteLine($"Ignoring corrupted high score file '{_filePath}': {ex.Message}");
            return new List<HighScoreEntry>();
        }
    }

    private static IReadOnlyList<HighScoreEntry> NormalizeAndTrimPerBucket(List<HighScoreEntry> allEntries)
    {
        var buckets = new Dictionary<(string Mode, string Difficulty), List<HighScoreEntry>>();
        for (int i = 0; i < allEntries.Count; i++)
        {
            HighScoreEntry e = NormalizeEntry(allEntries[i]);
            var key = (e.Mode.ToUpperInvariant(), e.Difficulty.ToUpperInvariant());
            if (!buckets.TryGetValue(key, out List<HighScoreEntry>? list))
            {
                list = new List<HighScoreEntry>(MAX_LEADERBOARD_ENTRIES_PER_BUCKET + 1);
                buckets[key] = list;
            }

            list.Add(e);
        }

        var result = new List<HighScoreEntry>(allEntries.Count);
        foreach (List<HighScoreEntry> bucket in buckets.Values)
        {
            bucket.Sort(CompareEntries);
            int take = Math.Min(MAX_LEADERBOARD_ENTRIES_PER_BUCKET, bucket.Count);
            for (int i = 0; i < take; i++)
            {
                result.Add(bucket[i]);
            }
        }

        result.Sort(CompareEntries);
        return result.AsReadOnly();
    }

    private static HighScoreEntry NormalizeFromRecord(HighScoreJsonRecord raw)
    {
        string islandId = string.IsNullOrWhiteSpace(raw.IslandId) ? "ISLAND_01" : raw.IslandId.Trim();
        string inferredMode = !string.IsNullOrWhiteSpace(raw.Mode)
            ? raw.Mode.Trim()
            : (islandId.StartsWith("ENDLESS_", StringComparison.OrdinalIgnoreCase) ? "Endless" : "Campaign");
        string difficulty = string.IsNullOrWhiteSpace(raw.Difficulty) ? "Normal" : raw.Difficulty.Trim();
        DateTime achievedUtc = raw.AchievedAtUtc.HasValue && raw.AchievedAtUtc.Value != default
            ? raw.AchievedAtUtc.Value.ToUniversalTime()
            : DateTime.UnixEpoch;
        double playTime = raw.PlayTimeSeconds.HasValue && raw.PlayTimeSeconds.Value >= 0.0
            ? raw.PlayTimeSeconds.Value
            : 0.0;

        return new HighScoreEntry(
            raw.PlayerName!.Trim(),
            raw.Score,
            islandId,
            difficulty,
            achievedUtc,
            inferredMode,
            playTime);
    }

    private static HighScoreEntry NormalizeEntry(HighScoreEntry entry)
    {
        string mode = string.IsNullOrWhiteSpace(entry.Mode)
            ? (entry.IslandId.StartsWith("ENDLESS_", StringComparison.OrdinalIgnoreCase) ? "Endless" : "Campaign")
            : entry.Mode.Trim();
        string difficulty = string.IsNullOrWhiteSpace(entry.Difficulty) ? "Normal" : entry.Difficulty.Trim();
        double playTime = double.IsNaN(entry.PlayTimeSeconds) || entry.PlayTimeSeconds < 0.0 ? 0.0 : entry.PlayTimeSeconds;
        DateTime achieved = entry.AchievedAtUtc == default ? DateTime.UtcNow : entry.AchievedAtUtc.ToUniversalTime();

        return new HighScoreEntry(
            entry.PlayerName.Trim(),
            entry.Score,
            entry.IslandId.Trim(),
            difficulty,
            achieved,
            mode,
            playTime);
    }

    private static int CompareEntries(HighScoreEntry a, HighScoreEntry b)
    {
        int scoreCmp = b.Score.CompareTo(a.Score);
        if (scoreCmp != 0)
        {
            return scoreCmp;
        }

        return a.AchievedAtUtc.CompareTo(b.AchievedAtUtc);
    }

    private static void ValidateEntry(HighScoreEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.PlayerName))
        {
            throw new ArgumentException("High score PlayerName cannot be empty.", nameof(entry));
        }

        if (entry.Score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entry), "High score cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(entry.IslandId))
        {
            throw new ArgumentException("High score IslandId cannot be empty.", nameof(entry));
        }
    }

    private void WriteEntriesAtomically(IReadOnlyList<HighScoreEntry> entries)
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = $"{_filePath}.tmp";
        try
        {
            string json = JsonSerializer.Serialize(entries, JSON_OPTIONS);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to write high score file '{_filePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Mutable DTO isolating JSON deserialization so legacy <c>highscore.json</c> files missing
    /// newer properties (<c>mode</c>, <c>playTimeSeconds</c>) safely deserialize with defaults.
    /// </summary>
    private sealed class HighScoreJsonRecord
    {
        public string? PlayerName { get; set; }
        public int Score { get; set; }
        public string? IslandId { get; set; }
        public string? Difficulty { get; set; }
        public DateTime? AchievedAtUtc { get; set; }
        public string? Mode { get; set; }
        public double? PlayTimeSeconds { get; set; }
    }
}
