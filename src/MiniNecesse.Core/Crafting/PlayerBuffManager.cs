using System;
using System.Collections.Generic;
using System.Text;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Tracks active temporary food buffs on a <see cref="Player"/> using reusable mutable slots
/// for zero per-tick GC allocations.
/// </summary>
public sealed class PlayerBuffManager
{
    /// <summary>Represents one active timed buff entry.</summary>
    public sealed class ActiveFoodBuff
    {
        public IFoodBuffStrategy Strategy { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        internal float RegenAccumulator { get; set; }

        internal ActiveFoodBuff(IFoodBuffStrategy strategy, float remainingSeconds)
        {
            Strategy = strategy;
            RemainingSeconds = remainingSeconds;
        }
    }

    private readonly List<ActiveFoodBuff> _activeBuffs = new(4);
    private readonly IReadOnlyList<ActiveFoodBuff> _activeBuffsView;
    private readonly StringBuilder _summaryBuilder = new(64);

    private float _speedMultiplier = 1f;
    private int _bonusDamage;
    private int _bonusDefense;
    private string _primaryBuffSummary = string.Empty;

    /// <summary>Gets a cached read-only list of active food buffs.</summary>
    public IReadOnlyList<ActiveFoodBuff> ActiveBuffs => _activeBuffsView;

    /// <summary>Gets the number of currently active food buffs.</summary>
    public int ActiveBuffCount => _activeBuffs.Count;

    /// <summary>Gets the combined movement speed multiplier across all active buffs.</summary>
    public float SpeedMultiplier => _speedMultiplier;

    /// <summary>Gets the combined bonus attack damage across all active buffs.</summary>
    public int BonusDamage => _bonusDamage;

    /// <summary>Gets the combined bonus defense across all active buffs.</summary>
    public int BonusDefense => _bonusDefense;

    /// <summary>Gets a cached human-readable summary of active buffs for HUD display (0 GC steady-state).</summary>
    public string PrimaryBuffSummary => _primaryBuffSummary;

    /// <summary>Raised whenever buffs are applied, refreshed, expired, or cleared.</summary>
    public event Action? OnBuffsChanged;

    /// <summary>Initializes a new instance of the <see cref="PlayerBuffManager"/> class.</summary>
    public PlayerBuffManager()
    {
        _activeBuffsView = _activeBuffs.AsReadOnly();
    }

    /// <summary>Applies a food buff strategy or refreshes its timer if already active.</summary>
    public void ApplyBuff(IFoodBuffStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            ActiveFoodBuff existing = _activeBuffs[i];
            if (string.Equals(existing.Strategy.BuffId, strategy.BuffId, StringComparison.OrdinalIgnoreCase))
            {
                existing.Strategy = strategy;
                existing.RemainingSeconds = strategy.DurationSeconds;
                existing.RegenAccumulator = 0f;
                RecalculateAggregates();
                OnBuffsChanged?.Invoke();
                return;
            }
        }

        _activeBuffs.Add(new ActiveFoodBuff(strategy, strategy.DurationSeconds));
        RecalculateAggregates();
        OnBuffsChanged?.Invoke();
    }

    /// <summary>Advances active buff timers, applies regeneration, and removes expired buffs.</summary>
    public void Update(float deltaTime, Player player)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(player);
        if (_activeBuffs.Count == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = _activeBuffs.Count - 1; i >= 0; i--)
        {
            ActiveFoodBuff entry = _activeBuffs[i];
            if (player.IsAlive && entry.Strategy.RegenPerSecond > 0f && deltaTime > 0f)
            {
                entry.RegenAccumulator += entry.Strategy.RegenPerSecond * deltaTime;
                if (entry.RegenAccumulator >= 1f)
                {
                    int healPoints = (int)MathF.Floor(entry.RegenAccumulator);
                    entry.RegenAccumulator -= healPoints;
                    player.Heal(healPoints);
                }
            }

            entry.RemainingSeconds -= deltaTime;
            if (entry.RemainingSeconds <= 0f)
            {
                _activeBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            RecalculateAggregates();
            OnBuffsChanged?.Invoke();
        }
    }

    /// <summary>Removes all active buffs.</summary>
    public void Clear()
    {
        if (_activeBuffs.Count == 0)
        {
            return;
        }

        _activeBuffs.Clear();
        RecalculateAggregates();
        OnBuffsChanged?.Invoke();
    }

    private void RecalculateAggregates()
    {
        float speed = 1f;
        int dmg = 0;
        int def = 0;

        _summaryBuilder.Clear();
        for (int i = 0; i < _activeBuffs.Count; i++)
        {
            IFoodBuffStrategy s = _activeBuffs[i].Strategy;
            speed *= s.SpeedMultiplier;
            dmg += s.BonusDamage;
            def += s.BonusDefense;

            if (i > 0)
            {
                _summaryBuilder.Append(" + ");
            }

            _summaryBuilder.Append(s.DisplayName);
        }

        _speedMultiplier = global::System.Math.Max(0.1f, speed);
        _bonusDamage = dmg;
        _bonusDefense = def;
        _primaryBuffSummary = _summaryBuilder.ToString();
    }
}
