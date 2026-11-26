using System;
using System.IO;
using System.Text.Json;
using MiniNecesse.App.Input;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.World;

namespace MiniNecesse.App.Settings;

/// <summary>
/// Application-level user preferences shared across menus, settings dialogs, and gameplay,
/// with optional JSON persistence in <c>Saves/settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Gets or sets the selected gameplay difficulty.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>Gets or sets whether audio output is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Gets or sets audio volume in [0, 100].</summary>
    public int Volume { get; set; } = 80;

    /// <summary>Gets or sets the default player name for high-score submissions.</summary>
    public string PlayerName { get; set; } = "Survivor";

    /// <summary>Gets or sets the chunk streaming view radius around the player (1..8 chunks).</summary>
    public int ViewDistanceChunks { get; set; } = ChunkManager.DEFAULT_VIEW_DISTANCE;

    /// <summary>Gets the active keyboard binding configuration.</summary>
    public KeyBindingConfig KeyBindings { get; } = KeyBindingConfig.LoadOrDefault();

    /// <summary>Loads persisted user settings from a relative JSON file, falling back safely to defaults.</summary>
    public static AppSettings LoadOrDefault(string? filePath = null)
    {
        string resolvedPath = ResolveSettingsPath(filePath);
        if (!File.Exists(resolvedPath))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            SettingsSnapshot? dto = JsonSerializer.Deserialize<SettingsSnapshot>(json, JSON_OPTIONS);
            if (dto is null)
            {
                return new AppSettings();
            }

            return new AppSettings
            {
                Difficulty = Enum.TryParse(dto.Difficulty, ignoreCase: true, out Difficulty parsedDiff)
                    ? parsedDiff
                    : Difficulty.Normal,
                IsMuted = dto.IsMuted,
                Volume = Math.Clamp(dto.Volume, 0, 100),
                PlayerName = string.IsNullOrWhiteSpace(dto.PlayerName) ? "Survivor" : dto.PlayerName.Trim(),
                ViewDistanceChunks = Math.Clamp(
                    dto.ViewDistanceChunks,
                    ChunkManager.DEFAULT_MIN_VIEW_DISTANCE,
                    ChunkManager.DEFAULT_MAX_VIEW_DISTANCE)
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            System.Diagnostics.Debug.WriteLine($"AppSettings fallback to defaults: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>Persists user settings to a relative JSON file.</summary>
    public void Save(string? filePath = null)
    {
        string resolvedPath = ResolveSettingsPath(filePath);
        string? directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = new SettingsSnapshot
        {
            Difficulty = Difficulty.ToString(),
            IsMuted = IsMuted,
            Volume = Math.Clamp(Volume, 0, 100),
            PlayerName = string.IsNullOrWhiteSpace(PlayerName) ? "Survivor" : PlayerName.Trim(),
            ViewDistanceChunks = Math.Clamp(
                ViewDistanceChunks,
                ChunkManager.DEFAULT_MIN_VIEW_DISTANCE,
                ChunkManager.DEFAULT_MAX_VIEW_DISTANCE)
        };

        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(snapshot, JSON_OPTIONS));
    }

    private static string ResolveSettingsPath(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, "Saves", "settings.json")
            : filePath;
    }

    private sealed class SettingsSnapshot
    {
        public string Difficulty { get; set; } = "Normal";
        public bool IsMuted { get; set; }
        public int Volume { get; set; } = 80;
        public string PlayerName { get; set; } = "Survivor";
        public int ViewDistanceChunks { get; set; } = ChunkManager.DEFAULT_VIEW_DISTANCE;
    }
}
