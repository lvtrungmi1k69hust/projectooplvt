using System;

namespace MiniNecesse.Core.Quests;

/// <summary>
/// Immutable survival quest loaded from <c>quests.json</c>.
/// </summary>
public sealed record QuestDefinition
{
    public string QuestId { get; }
    public string Title { get; }
    public string Description { get; }
    public QuestObjectiveType ObjectiveType { get; }
    public string TargetPattern { get; }
    public int RequiredCount { get; }
    public int RewardScore { get; }
    public string? RewardItemId { get; }
    public int RewardQuantity { get; }

    public QuestDefinition(
        string questId,
        string title,
        string description,
        QuestObjectiveType objectiveType,
        string targetPattern,
        int requiredCount,
        int rewardScore,
        string? rewardItemId = null,
        int rewardQuantity = 0)
    {
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Quest id and title cannot be empty.");
        }

        if (requiredCount <= 0 || rewardScore < 0 || rewardQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredCount), "Quest count and rewards must be valid.");
        }

        QuestId = questId.Trim();
        Title = title.Trim();
        Description = description ?? string.Empty;
        ObjectiveType = objectiveType;
        TargetPattern = string.IsNullOrWhiteSpace(targetPattern) ? "*" : targetPattern.Trim();
        RequiredCount = requiredCount;
        RewardScore = rewardScore;
        RewardItemId = string.IsNullOrWhiteSpace(rewardItemId) ? null : rewardItemId.Trim();
        RewardQuantity = rewardQuantity;
    }
}
