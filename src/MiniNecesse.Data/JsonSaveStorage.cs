using System;
using System.IO;
using System.Text.Json;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Data;

/// <summary>
/// Persists and restores complete <see cref="GameStateDTO"/> snapshots into relative JSON save files
/// (e.g., <c>save_slot_1.json</c>). Implements <see cref="ISaveStorage"/> with strict schema validation
/// and atomic file writes.
/// </summary>
public sealed class JsonSaveStorage : ISaveStorage
{
    private const string DEFAULT_SAVES_FOLDER = "Saves";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _savesDirectory;

    /// <summary>Gets the directory where slot save files are stored.</summary>
    public string SavesDirectory => _savesDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSaveStorage"/> class.
    /// Defaults to a relative <c>Saves/</c> folder beside the application executable.
    /// </summary>
    public JsonSaveStorage(string? savesDirectory = null)
    {
        _savesDirectory = string.IsNullOrWhiteSpace(savesDirectory)
            ? Path.Combine(AppContext.BaseDirectory, DEFAULT_SAVES_FOLDER)
            : savesDirectory;
    }

    /// <summary>Resolves the file path for the specified 1-based save slot (e.g., save_slot_1.json).</summary>
    public string GetSlotFilePath(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);
        return Path.Combine(_savesDirectory, $"save_slot_{slotIndex}.json");
    }

    /// <inheritdoc />
    public void Save(int slotIndex, GameStateDTO snapshot)
    {
        ValidateSlotIndex(slotIndex);
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshotOrThrow(snapshot, slotIndex);

        Directory.CreateDirectory(_savesDirectory);
        string targetPath = GetSlotFilePath(slotIndex);
        string tempPath = $"{targetPath}.tmp";

        try
        {
            string json = JsonSerializer.Serialize(snapshot, JSON_OPTIONS);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch (IOException ex)
        {
            throw new SaveCorruptedException($"Failed to write save slot {slotIndex}: {ex.Message}", ex, slotIndex);
        }
    }

    /// <inheritdoc />
    public GameStateDTO Load(int slotIndex)
    {
        string targetPath = GetSlotFilePath(slotIndex);
        if (!File.Exists(targetPath))
        {
            throw new SaveCorruptedException($"Save file for slot {slotIndex} does not exist.", slotIndex);
        }

        try
        {
            string json = File.ReadAllText(targetPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new SaveCorruptedException($"Save slot {slotIndex} is empty.", slotIndex);
            }

            GameStateDTO? snapshot = JsonSerializer.Deserialize<GameStateDTO>(json, JSON_OPTIONS);
            if (snapshot is null)
            {
                throw new SaveCorruptedException($"Save slot {slotIndex} deserialized to null.", slotIndex);
            }

            ValidateSnapshotOrThrow(snapshot, slotIndex);
            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new SaveCorruptedException($"Corrupted JSON in save slot {slotIndex}: {ex.Message}", ex, slotIndex);
        }
        catch (IOException ex)
        {
            throw new SaveCorruptedException($"Unable to read save slot {slotIndex}: {ex.Message}", ex, slotIndex);
        }
    }

    /// <inheritdoc />
    public bool Exists(int slotIndex) => File.Exists(GetSlotFilePath(slotIndex));

    /// <inheritdoc />
    public void Delete(int slotIndex)
    {
        string targetPath = GetSlotFilePath(slotIndex);
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }
    }

    private static void ValidateSlotIndex(int slotIndex)
    {
        if (slotIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Save slot index must be >= 1.");
        }
    }

    private static void ValidateSnapshotOrThrow(GameStateDTO snapshot, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(snapshot.IslandId))
        {
            throw new SaveCorruptedException("Save data is missing required 'islandId'.", slotIndex);
        }

        if (snapshot.Score < 0 || snapshot.DayNumber <= 0 || snapshot.TimeOfDaySeconds < 0f || snapshot.CurrentWaveIndex < 0)
        {
            throw new SaveCorruptedException("Save data contains invalid negative or zero progression fields.", slotIndex);
        }

        ValidatePlayerSnapshot(snapshot.Player, slotIndex);
        ValidateWorldSnapshots(snapshot, slotIndex);
    }

    private static void ValidatePlayerSnapshot(PlayerSnapshot? player, int slotIndex)
    {
        if (player is null || player.MaxHp <= 0 || player.Hp < 0 || player.Hp > player.MaxHp || player.SelectedHotbarIndex < 0)
        {
            throw new SaveCorruptedException("Save data contains invalid player health or hotbar state.", slotIndex);
        }

        if (player.Inventory is null)
        {
            throw new SaveCorruptedException("Save data is missing player inventory collection.", slotIndex);
        }

        for (int i = 0; i < player.Inventory.Count; i++)
        {
            InventoryItemSnapshot item = player.Inventory[i];
            if (string.IsNullOrWhiteSpace(item.ItemId) || item.Quantity <= 0)
            {
                throw new SaveCorruptedException($"Invalid inventory entry at index {i}.", slotIndex);
            }
        }
    }

    private static void ValidateWorldSnapshots(GameStateDTO snapshot, int slotIndex)
    {
        if (snapshot.Resources is null || snapshot.Structures is null || snapshot.Enemies is null)
        {
            throw new SaveCorruptedException("Save data is missing world entity lists.", slotIndex);
        }

        for (int i = 0; i < snapshot.Resources.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Resources[i].Type) || snapshot.Resources[i].Hp < 0)
            {
                throw new SaveCorruptedException($"Invalid resource snapshot at index {i}.", slotIndex);
            }
        }

        for (int i = 0; i < snapshot.Structures.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Structures[i].Type) || snapshot.Structures[i].Hp < 0)
            {
                throw new SaveCorruptedException($"Invalid structure snapshot at index {i}.", slotIndex);
            }
        }

        for (int i = 0; i < snapshot.Enemies.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Enemies[i].Type) || snapshot.Enemies[i].Hp < 0)
            {
                throw new SaveCorruptedException($"Invalid enemy snapshot at index {i}.", slotIndex);
            }
        }
    }
}
