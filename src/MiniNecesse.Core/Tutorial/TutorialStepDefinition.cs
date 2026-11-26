using System;

namespace MiniNecesse.Core.Tutorial;

/// <summary>
/// Immutable tutorial step loaded from <c>tutorial_steps.json</c>.
/// </summary>
public sealed record TutorialStepDefinition
{
    public string StepId { get; }
    public string Title { get; }
    public string Instruction { get; }
    public TutorialTriggerType TriggerType { get; }
    public string TargetPattern { get; }

    public TutorialStepDefinition(
        string stepId,
        string title,
        string instruction,
        TutorialTriggerType triggerType,
        string targetPattern)
    {
        if (string.IsNullOrWhiteSpace(stepId) || string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Tutorial step id and title cannot be empty.");
        }

        StepId = stepId.Trim();
        Title = title.Trim();
        Instruction = instruction ?? string.Empty;
        TriggerType = triggerType;
        TargetPattern = string.IsNullOrWhiteSpace(targetPattern) ? "*" : targetPattern.Trim();
    }
}
