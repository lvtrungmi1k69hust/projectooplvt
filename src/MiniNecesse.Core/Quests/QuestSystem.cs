using System;
using System.Collections.Generic;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Quests;

/// <summary>
/// Zero-LINQ survival quest coordinator managing JSON quest progression, rewards, and save snapshots.
/// </summary>
public sealed class QuestSystem
{
    private readonly List<QuestProgress> _quests = new();
    private readonly IReadOnlyList<QuestProgress> _questsView;
    private readonly Action<int, string?, int> _rewardDispatcher;

    public IReadOnlyList<QuestProgress> Quests => _questsView;

    /// <summary>Gets the first uncompleted quest, or <see langword="null"/> when all quests are finished.</summary>
    public QuestProgress? ActiveQuest
    {
        get
        {
            for (int i = 0; i < _quests.Count; i++)
            {
                if (!_quests[i].IsCompleted)
                {
                    return _quests[i];
                }
            }

            return null;
        }
    }

    public int CompletedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _quests.Count; i++)
            {
                if (_quests[i].IsCompleted)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public event Action<QuestDefinition>? OnQuestCompleted;

    public QuestSystem(Action<int, string?, int> rewardDispatcher, IReadOnlyList<QuestDefinition>? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(rewardDispatcher);
        _rewardDispatcher = rewardDispatcher;

        IReadOnlyList<QuestDefinition> source = definitions is { Count: > 0 } ? definitions : CreateDefaultQuests();
        for (int i = 0; i < source.Count; i++)
        {
            _quests.Add(new QuestProgress(source[i]));
        }

        _questsView = _quests.AsReadOnly();
    }

    public int NotifyAction(QuestObjectiveType objectiveType, string targetName, int amount = 1)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int newlyCompleted = 0;
        for (int i = 0; i < _quests.Count; i++)
        {
            QuestProgress quest = _quests[i];
            if (quest.IsCompleted || quest.Definition.ObjectiveType != objectiveType || !Matches(targetName, quest.Definition.TargetPattern))
            {
                continue;
            }

            if (quest.TryAdvance(amount) && quest.IsCompleted)
            {
                newlyCompleted++;
                _rewardDispatcher(quest.Definition.RewardScore, quest.Definition.RewardItemId, quest.Definition.RewardQuantity);
                OnQuestCompleted?.Invoke(quest.Definition);
            }
        }

        return newlyCompleted;
    }

    public void PopulateSnapshots(List<QuestProgressSnapshot> output)
    {
        output.Clear();
        for (int i = 0; i < _quests.Count; i++)
        {
            output.Add(new QuestProgressSnapshot
            {
                QuestId = _quests[i].Definition.QuestId,
                CurrentCount = _quests[i].CurrentCount
            });
        }
    }

    public void RestoreSnapshots(IReadOnlyList<QuestProgressSnapshot>? snapshots)
    {
        for (int i = 0; i < _quests.Count; i++)
        {
            _quests[i].RestoreCount(0);
        }

        if (snapshots is null)
        {
            return;
        }

        for (int s = 0; s < snapshots.Count; s++)
        {
            QuestProgressSnapshot snap = snapshots[s];
            for (int i = 0; i < _quests.Count; i++)
            {
                if (string.Equals(_quests[i].Definition.QuestId, snap.QuestId, StringComparison.OrdinalIgnoreCase))
                {
                    _quests[i].RestoreCount(snap.CurrentCount);
                    break;
                }
            }
        }
    }

    public static IReadOnlyList<QuestDefinition> CreateDefaultQuests()
    {
        return new[]
        {
            new QuestDefinition("gather_wood", "Gather Timber", "Harvest 3 Trees for wood.", QuestObjectiveType.HarvestResource, "Tree", 3, 100, "bread", 1),
            new QuestDefinition("build_shelter", "Fortify Base", "Place 4 structures or walls.", QuestObjectiveType.PlaceStructure, "*", 4, 150, "torch", 2),
            new QuestDefinition("slay_hostiles", "Island Defender", "Defeat 3 hostile monsters.", QuestObjectiveType.DefeatEnemy, "*", 3, 200, "health_potion", 1),
            new QuestDefinition("harvest_crops", "Homestead Harvest", "Harvest 2 grown farm crops.", QuestObjectiveType.HarvestCrop, "*", 2, 150, "cooked_meat", 2)
        };
    }

    private static bool Matches(string targetName, string pattern)
    {
        if (pattern == "*") return true;
        if (string.IsNullOrWhiteSpace(targetName)) return false;
        return string.Equals(targetName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
