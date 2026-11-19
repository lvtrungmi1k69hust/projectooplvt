using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Data;

/// <summary>
/// Persists and loads the Top 10 high-score leaderboard in <c>highscore.json</c>.
/// Safely returns an empty list when no file exists yet and guards against corrupt JSON.
/// </summary>
public sealed class JsonHighScoreRepository : IHighScoreRepository
{
    private const int MAX_LEADERBOARD_ENTRIES = 10;
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
        if (!File.Exists(_filePath))
        {
            return Array.Empty<HighScoreEntry>();
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<HighScoreEntry>();
            }

            List<HighScoreEntry>? entries = JsonSerializer.Deserialize<List<HighScoreEntry>>(json, JSON_OPTIONS);
            if (entries is null)
            {
                return Array.Empty<HighScoreEntry>();
            }

            return NormalizeAndTrim(entries);
        }
        catch (JsonException ex)
        {
            throw new SaveCorruptedException($"High score file '{_filePath}' contains invalid JSON: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new SaveCorruptedException($"Failed to read high score file '{_filePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public void RecordScore(HighScoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);

        var updated = new List<HighScoreEntry>(LoadTopScores()) { entry };
        IReadOnlyList<HighScoreEntry> topEntries = NormalizeAndTrim(updated);

        WriteEntriesAtomically(topEntries);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
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

    private static IReadOnlyList<HighScoreEntry> NormalizeAndTrim(IEnumerable<HighScoreEntry> entries)
    {
        return entries
            .Where(e => e is not null && e.Score >= 0 && !string.IsNullOrWhiteSpace(e.PlayerName))
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.AchievedAtUtc)
            .Take(MAX_LEADERBOARD_ENTRIES)
            .ToList()
            .AsReadOnly();
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
            throw new SaveCorruptedException($"Failed to write high score file '{_filePath}': {ex.Message}", ex);
        }
    }
}
