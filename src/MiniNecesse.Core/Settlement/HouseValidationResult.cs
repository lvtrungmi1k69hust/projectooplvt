using MiniNecesse.Core.AI;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Immutable housing inspection result indicating whether an enclosed shelter meets settler requirements.
/// </summary>
public sealed record HouseValidationResult(
    bool IsValid,
    int InteriorTileCount,
    int DoorCount,
    int TorchCount,
    GridPoint AnchorTile,
    string Reason);
