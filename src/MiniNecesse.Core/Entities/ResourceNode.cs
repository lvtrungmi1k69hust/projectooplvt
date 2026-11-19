using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a natural harvestable resource deposit (Tree, Stone Rock, Copper/Iron/Gold Vein).
/// Implements both <see cref="IDamageable"/> and <see cref="IHarvestable"/>.
/// </summary>
public sealed class ResourceNode : Entity, IDamageable, IHarvestable
{
    private const int WRONG_TOOL_DIVISOR = 4;

    private readonly ResourceType _resourceType;
    private readonly ToolType _requiredTool;
    private readonly string _dropItemId;
    private readonly int _dropQuantity;
    private readonly int _maxHp;
    private int _currentHp;

    /// <summary>Gets the natural resource archetype of this node.</summary>
    public ResourceType ResourceType => _resourceType;

    /// <inheritdoc />
    public ToolType RequiredTool => _requiredTool;

    /// <inheritdoc />
    public string DropItemId => _dropItemId;

    /// <inheritdoc />
    public int DropQuantity => _dropQuantity;

    /// <inheritdoc />
    public int CurrentHp => _currentHp;

    /// <inheritdoc />
    public int MaxHp => _maxHp;

    /// <inheritdoc />
    public bool IsAlive => _currentHp > 0;

    /// <inheritdoc />
    public event Action<int, int>? OnHealthChanged;

    /// <inheritdoc />
    public event Action<IDamageable>? OnDefeated;

    /// <inheritdoc />
    public event Action<IHarvestable, string, int>? OnHarvested;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNode"/> class.
    /// </summary>
    public ResourceNode(
        string id,
        ResourceType resourceType,
        Vector2D position,
        int maxHp,
        ToolType requiredTool,
        string dropItemId,
        int dropQuantity,
        Vector2D? size = null)
        : base(id, position, size ?? new Vector2D(28f, 28f))
    {
        if (maxHp <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Resource max HP must be positive.");
        }

        if (string.IsNullOrWhiteSpace(dropItemId))
        {
            throw new ArgumentException("Drop item id cannot be empty.", nameof(dropItemId));
        }

        if (dropQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dropQuantity), "Drop quantity must be positive.");
        }

        _resourceType = resourceType;
        _maxHp = maxHp;
        _currentHp = maxHp;
        _requiredTool = requiredTool;
        _dropItemId = dropItemId;
        _dropQuantity = dropQuantity;
    }

    /// <inheritdoc />
    public bool CanHarvestWith(ToolType toolType) => _requiredTool == ToolType.None || toolType == _requiredTool;

    /// <inheritdoc />
    public int Harvest(ToolType toolType, int toolPower)
    {
        if (toolPower < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toolPower), "Tool power cannot be negative.");
        }

        if (toolPower == 0 || !IsAlive)
        {
            return 0;
        }

        int effectiveDamage = CanHarvestWith(toolType)
            ? toolPower
            : global::System.Math.Max(1, toolPower / WRONG_TOOL_DIVISOR);

        TakeDamage(effectiveDamage);
        return effectiveDamage;
    }

    /// <inheritdoc />
    public void TakeDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Damage amount cannot be negative.");
        }

        if (amount == 0 || !IsAlive)
        {
            return;
        }

        _currentHp = global::System.Math.Max(0, _currentHp - amount);
        OnHealthChanged?.Invoke(_currentHp, _maxHp);

        if (_currentHp == 0)
        {
            Deactivate();
            OnHarvested?.Invoke(this, _dropItemId, _dropQuantity);
            OnDefeated?.Invoke(this);
        }
    }

    /// <inheritdoc />
    public void Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Heal amount cannot be negative.");
        }

        if (amount == 0 || !IsAlive || _currentHp == _maxHp)
        {
            return;
        }

        _currentHp = global::System.Math.Min(_maxHp, _currentHp + amount);
        OnHealthChanged?.Invoke(_currentHp, _maxHp);
    }

    /// <summary>Restores exact health within [0, MaxHp] during save-state loading.</summary>
    public void RestoreHealth(int hp)
    {
        if (hp < 0 || hp > _maxHp)
        {
            throw new ArgumentOutOfRangeException(nameof(hp), "Restored HP must be within [0, MaxHp].");
        }

        _currentHp = hp;
        if (_currentHp == 0)
        {
            Deactivate();
        }
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        // Static world resource nodes do not move on fixed ticks.
    }
}
