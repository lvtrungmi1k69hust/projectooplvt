using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Persistence abstraction (DIP) for saving and loading complete game session snapshots.
/// </summary>
public interface ISaveStorage
{
    /// <summary>Persists <paramref name="snapshot"/> into the given save slot.</summary>
    /// <param name="slotIndex">1-based save slot number.</param>
    /// <param name="snapshot">Complete game session snapshot.</param>
    void Save(int slotIndex, GameStateSnapshot snapshot);

    /// <summary>Loads and validates a saved session snapshot from the given slot.</summary>
    /// <param name="slotIndex">1-based save slot number.</param>
    /// <returns>The deserialized <see cref="GameStateSnapshot"/>.</returns>
    GameStateSnapshot Load(int slotIndex);

    /// <summary>Checks whether a save file exists for the given slot.</summary>
    bool Exists(int slotIndex);

    /// <summary>Deletes the save file for the given slot if it exists.</summary>
    void Delete(int slotIndex);
}
