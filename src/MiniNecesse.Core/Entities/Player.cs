using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents the player-controlled survivor character with hotbar selection,
/// equipped weapon/tool references, and attack cooldown management.
/// </summary>
public sealed class Player : Character
{
    private const int DEFAULT_HOTBAR_CAPACITY = 9;
    private const float DEFAULT_BARE_HAND_COOLDOWN = 0.45f;
    private const int DEFAULT_BARE_HAND_HARVEST_POWER = 5;

    private readonly int _hotbarCapacity;
    private int _selectedHotbarIndex;
    private IWeapon? _equippedWeapon;
    private ToolType _equippedToolType;
    private int _harvestPower;
    private float _attackCooldownRemaining;

    /// <summary>Gets the number of hotbar slots available to the player.</summary>
    public int HotbarCapacity => _hotbarCapacity;

    /// <summary>Gets the 0-based index of the currently selected hotbar slot.</summary>
    public int SelectedHotbarIndex => _selectedHotbarIndex;

    /// <summary>Gets the currently equipped combat weapon, if any.</summary>
    public IWeapon? EquippedWeapon => _equippedWeapon;

    /// <summary>Gets the currently equipped harvesting tool archetype.</summary>
    public ToolType EquippedToolType => _equippedToolType;

    /// <summary>Gets the harvesting power of the currently equipped tool.</summary>
    public int HarvestPower => _harvestPower;

    /// <summary>Gets remaining seconds before the next attack or swing can execute.</summary>
    public float AttackCooldownRemaining => _attackCooldownRemaining;

    /// <summary>Gets a value indicating whether the player can attack or swing right now.</summary>
    public bool CanAct => IsAlive && _attackCooldownRemaining <= 0f;

    /// <summary>Raised when the player switches the active hotbar slot.</summary>
    public event Action<int>? OnHotbarSelectionChanged;

    /// <summary>Raised when the equipped weapon changes.</summary>
    public event Action<IWeapon?>? OnWeaponEquipped;

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// </summary>
    public Player(
        string id,
        Vector2D spawnPosition,
        int maxHp = 100,
        float moveSpeed = 140f,
        int hotbarCapacity = DEFAULT_HOTBAR_CAPACITY)
        : base(id, spawnPosition, new Vector2D(24f, 24f), maxHp, moveSpeed)
    {
        if (hotbarCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hotbarCapacity), "Hotbar capacity must be positive.");
        }

        _hotbarCapacity = hotbarCapacity;
        _selectedHotbarIndex = 0;
        _equippedToolType = ToolType.None;
        _harvestPower = DEFAULT_BARE_HAND_HARVEST_POWER;
    }

    /// <summary>Equips a weapon implementing <see cref="IWeapon"/> (Association).</summary>
    public void EquipWeapon(IWeapon? weapon)
    {
        _equippedWeapon = weapon;
        OnWeaponEquipped?.Invoke(_equippedWeapon);
    }

    /// <summary>Equips a harvesting tool archetype and power level.</summary>
    public void EquipTool(ToolType toolType, int harvestPower)
    {
        if (harvestPower <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(harvestPower), "Harvest power must be positive.");
        }

        _equippedToolType = toolType;
        _harvestPower = harvestPower;
    }

    /// <summary>Clears any equipped tool back to bare hands.</summary>
    public void UnequipTool()
    {
        _equippedToolType = ToolType.None;
        _harvestPower = DEFAULT_BARE_HAND_HARVEST_POWER;
    }

    /// <summary>Selects a valid 0-based hotbar slot index.</summary>
    public void SelectHotbarSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _hotbarCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Hotbar slot index is out of range.");
        }

        if (_selectedHotbarIndex == slotIndex)
        {
            return;
        }

        _selectedHotbarIndex = slotIndex;
        OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex);
    }

    /// <summary>Starts the attack or tool swing cooldown using the equipped weapon or default timing.</summary>
    public void TriggerActionCooldown(float? customCooldown = null)
    {
        float cooldown = customCooldown ?? _equippedWeapon?.ComputeCooldown() ?? DEFAULT_BARE_HAND_COOLDOWN;
        _attackCooldownRemaining = global::System.Math.Max(0f, cooldown);
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (_attackCooldownRemaining > 0f)
        {
            _attackCooldownRemaining = global::System.Math.Max(0f, _attackCooldownRemaining - deltaTime);
        }
    }
}
