using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Tutorial;

/// <summary>
/// Zero-LINQ stateful checklist tracker guiding players step by step through data-driven
/// onboarding goals when enabled by the active difficulty profile.
/// </summary>
public sealed class TutorialTracker
{
    private readonly List<TutorialStepDefinition> _steps;
    private readonly IReadOnlyList<TutorialStepDefinition> _stepsView;
    private bool _isEnabled;
    private int _currentStepIndex;

    public bool IsEnabled => _isEnabled;
    public IReadOnlyList<TutorialStepDefinition> Steps => _stepsView;
    public int CurrentStepIndex => _currentStepIndex;
    public bool IsCompleted => !_isEnabled || _currentStepIndex >= _steps.Count;
    public TutorialStepDefinition? CurrentStep =>
        (_isEnabled && _currentStepIndex >= 0 && _currentStepIndex < _steps.Count)
            ? _steps[_currentStepIndex]
            : null;

    public event Action<TutorialStepDefinition>? OnStepCompleted;
    public event Action? OnTutorialCompleted;

    public TutorialTracker(bool isEnabled, IReadOnlyList<TutorialStepDefinition>? steps = null)
    {
        _isEnabled = isEnabled;
        _steps = new List<TutorialStepDefinition>(steps is { Count: > 0 } ? steps : CreateDefaultSteps());
        _stepsView = _steps.AsReadOnly();
        _currentStepIndex = 0;
    }

    public bool NotifyAction(TutorialTriggerType triggerType, string targetName)
    {
        TutorialStepDefinition? active = CurrentStep;
        if (active is null || active.TriggerType != triggerType || !MatchesPattern(targetName, active.TargetPattern))
        {
            return false;
        }

        _currentStepIndex++;
        OnStepCompleted?.Invoke(active);
        if (_currentStepIndex >= _steps.Count)
        {
            OnTutorialCompleted?.Invoke();
        }

        return true;
    }

    public void RestoreProgress(int completedStepIndex)
    {
        _currentStepIndex = global::System.Math.Clamp(completedStepIndex, 0, _steps.Count);
    }

    internal void SetEnabled(bool enabled) => _isEnabled = enabled;

    public static IReadOnlyList<TutorialStepDefinition> CreateDefaultSteps()
    {
        return new[]
        {
            new TutorialStepDefinition("chop_tree", "Chop a Tree", "Select your Axe and Left-Click a tree to gather Wood.", TutorialTriggerType.HarvestResource, "Tree"),
            new TutorialStepDefinition("craft_workbench", "Craft a Workbench", "Press [C] to craft a Workbench.", TutorialTriggerType.CraftItem, "workbench"),
            new TutorialStepDefinition("craft_sword", "Forge a Sword", "Place your Workbench and craft a Sword nearby.", TutorialTriggerType.CraftItem, "*sword*"),
            new TutorialStepDefinition("attack_enemy", "Strike an Enemy", "Equip your Sword and attack an enemy.", TutorialTriggerType.DamageEnemy, "*"),
            new TutorialStepDefinition("place_wall", "Build a Wall", "Select a wall and Right-Click to place shelter walls.", TutorialTriggerType.PlaceStructure, "*Wall*")
        };
    }

    private static bool MatchesPattern(string targetName, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        bool startsWild = pattern.StartsWith('*');
        bool endsWild = pattern.EndsWith('*');
        if (startsWild && endsWild && pattern.Length > 2)
        {
            string mid = pattern.Substring(1, pattern.Length - 2);
            return targetName.Contains(mid, StringComparison.OrdinalIgnoreCase);
        }

        if (startsWild && pattern.Length > 1)
        {
            return targetName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
        }

        if (endsWild && pattern.Length > 1)
        {
            return targetName.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(targetName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
