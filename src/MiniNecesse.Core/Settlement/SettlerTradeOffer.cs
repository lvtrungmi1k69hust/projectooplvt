using System;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Immutable barter trade offer offered by a recruited settler NPC.
/// </summary>
public sealed record SettlerTradeOffer
{
    public string OfferId { get; }
    public string CostItemId { get; }
    public int CostQuantity { get; }
    public string RewardItemId { get; }
    public int RewardQuantity { get; }

    public SettlerTradeOffer(
        string offerId,
        string costItemId,
        int costQuantity,
        string rewardItemId,
        int rewardQuantity)
    {
        if (string.IsNullOrWhiteSpace(offerId) ||
            string.IsNullOrWhiteSpace(costItemId) ||
            string.IsNullOrWhiteSpace(rewardItemId))
        {
            throw new ArgumentException("Trade offer identifiers cannot be empty.");
        }

        if (costQuantity <= 0 || rewardQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costQuantity), "Trade quantities must be positive.");
        }

        OfferId = offerId;
        CostItemId = costItemId;
        CostQuantity = costQuantity;
        RewardItemId = rewardItemId;
        RewardQuantity = rewardQuantity;
    }
}
