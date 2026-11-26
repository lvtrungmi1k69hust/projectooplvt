namespace MiniNecesse.Core.Quests;

/// <summary>
/// Mutable runtime progress entry for one <see cref="QuestDefinition"/>.
/// </summary>
public sealed class QuestProgress
{
    public QuestDefinition Definition { get; }
    public int CurrentCount { get; private set; }
    public bool IsCompleted => CurrentCount >= Definition.RequiredCount;

    public QuestProgress(QuestDefinition definition, int currentCount = 0)
    {
        Definition = definition;
        CurrentCount = global::System.Math.Clamp(currentCount, 0, definition.RequiredCount);
    }

    public bool TryAdvance(int amount = 1)
    {
        if (IsCompleted || amount <= 0)
        {
            return false;
        }

        CurrentCount = global::System.Math.Min(Definition.RequiredCount, CurrentCount + amount);
        return true;
    }

    public void RestoreCount(int count) =>
        CurrentCount = global::System.Math.Clamp(count, 0, Definition.RequiredCount);
}
