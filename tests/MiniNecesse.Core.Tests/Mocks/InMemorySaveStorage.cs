using System.Collections.Generic;
using System.Text.Json;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Tests.Mocks;

/// <summary>
/// In-memory test double implementing <see cref="ISaveStorage"/> with JSON round-trip cloning
/// so save/load logic can be verified cleanly without touching the filesystem.
/// </summary>
public sealed class InMemorySaveStorage : ISaveStorage
{
    private readonly Dictionary<int, string> _slots = new();

    public void Save(int slotIndex, GameStateDTO snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _slots[slotIndex] = JsonSerializer.Serialize(snapshot);
    }

    public GameStateDTO Load(int slotIndex)
    {
        if (!_slots.TryGetValue(slotIndex, out string? rawJson))
        {
            throw new SaveCorruptedException($"In-memory slot {slotIndex} does not exist.", slotIndex);
        }

        try
        {
            return JsonSerializer.Deserialize<GameStateDTO>(rawJson)
                ?? throw new SaveCorruptedException($"In-memory slot {slotIndex} deserialized to null.", slotIndex);
        }
        catch (JsonException ex)
        {
            throw new SaveCorruptedException($"In-memory slot {slotIndex} contains corrupt JSON.", ex, slotIndex);
        }
    }

    public bool Exists(int slotIndex) => _slots.ContainsKey(slotIndex);

    public void Delete(int slotIndex) => _slots.Remove(slotIndex);

    /// <summary>Injects malformed raw text into a slot to simulate save corruption.</summary>
    public void InjectRawCorruptData(int slotIndex, string corruptPayload) => _slots[slotIndex] = corruptPayload;
}
