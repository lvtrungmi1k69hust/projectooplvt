using System;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Decorator;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents the player-controlled survivor character with inventory aggregation,
/// hotbar selection, equipped weapon/tool references, hunger/starvation tracking, and food buff management.
/// </summary>
public sealed class Player : Character
{
    private const int DEFAULT_HOTBAR_CAPACITY = 9;
    private const int DEFAULT_INVENTORY_CAPACITY = 20;
    private const float DEFAULT_BARE_HAND_COOLDOWN = 0.45f;
    private const int DEFAULT_BARE_HAND_HARVEST_POWER = 5;
    private const float DEFAULT_MAX_HUNGER = 100f;
    private const float DEFAULT_HUNGER_DRAIN_PER_SEC = 0.9f;
    private const float DEFAULT_STARVATION_INTERVAL = 2.5f;
    private const int DEFAULT_STARVATION_DAMAGE = 3;

    private readonly Inventory _inventory;
    private readonly EquipmentLoadout _equipment = new();
    private readonly PlayerBuffManager _buffs = new();
    private IDamageMitigationStrategy _damageMitigationStrategy = FlatDefenseMitigationStrategy.Instance;
    private readonly int _hotbarCapacity;
    private readonly float _hungerDrainPerSecond;
    private readonly float _starvationInterval;
    private readonly int _starvationDamage;
    private readonly float _minHungerSpeedMultiplier;

    private int _selectedHotbarIndex;
    private IWeapon? _equippedWeapon;
    private IWeapon? _effectiveEquippedWeapon;
    private ToolType _equippedToolType;
    private int _harvestPower;
    private float _attackCooldownRemaining;

    private float _maxHunger;
    private float _currentHunger;
    private float _starvationTimer;
    private int _lastNotifiedHungerInt;

    /// <summary>Gets the player's inventory (UML Aggregation).</summary>
    public Inventory Inventory => _inventory;

    /// <summary>Gets the equipped Head/Body/Legs armor loadout.</summary>
    public EquipmentLoadout Equipment => _equipment;

    /// <summary>Gets the active food buff manager for this player.</summary>
    public PlayerBuffManager Buffs => _buffs;

    /// <summary>Gets the number of hotbar slots available to the player.</summary>
    public int HotbarCapacity => _hotbarCapacity;

    /// <summary>Gets the 0-based index of the currently selected hotbar slot.</summary>
    public int SelectedHotbarIndex => _selectedHotbarIndex;

    /// <summary>Gets the base currently equipped combat weapon, if any.</summary>
    public IWeapon? EquippedWeapon => _equippedWeapon;

    /// <summary>
    /// Gets the equipped weapon wrapped dynamically with <see cref="FoodDamageBuffDecorator"/>
    /// whenever active food buffs grant bonus attack damage (0 allocations during combat).
    /// </summary>
    public IWeapon? EffectiveEquippedWeapon => _effectiveEquippedWeapon;

    /// <summary>Gets the currently equipped harvesting tool archetype.</summary>
    public ToolType EquippedToolType => _equippedToolType;

    /// <summary>Gets the harvesting power of the currently equipped tool.</summary>
    public int HarvestPower => _harvestPower;

    /// <summary>Gets remaining seconds before the next attack or swing can execute.</summary>
    public float AttackCooldownRemaining => _attackCooldownRemaining;

    /// <summary>Gets a value indicating whether the player can attack or swing right now.</summary>
    public bool CanAct => IsAlive && _attackCooldownRemaining <= 0f;

    /// <summary>Gets movement speed factor induced by current hunger (0.52x at 0 hunger, ~0.76x at 50, 1.0x at 100).</summary>
    public float HungerSpeedMultiplier => _minHungerSpeedMultiplier + ((1f - _minHungerSpeedMultiplier) * HungerRatio);

    /// <summary>Gets effective movement speed scaled by hunger stamina and active food buffs.</summary>
    public float EffectiveMoveSpeed => MoveSpeed * HungerSpeedMultiplier * _buffs.SpeedMultiplier;

    /// <summary>Gets bonus attack damage from active food buffs.</summary>
    public int BonusAttackDamage => _buffs.BonusDamage;

    /// <summary>Gets bonus defense from active food buffs.</summary>
    public int BonusDefense => _buffs.BonusDefense;

    /// <summary>Gets total defense combining equipped armor and active food buffs.</summary>
    public int TotalDefense => _equipment.TotalDefense + _buffs.BonusDefense;

    /// <summary>Gets maximum hunger points.</summary>
    public float MaxHunger => _maxHunger;

    /// <summary>Gets current hunger points within [0, MaxHunger].</summary>
    public float CurrentHunger => _currentHunger;

    /// <summary>Gets normalized hunger ratio within [0, 1].</summary>
    public float HungerRatio => _maxHunger > 0f ? _currentHunger / _maxHunger : 0f;

    /// <summary>Gets whether the player's hunger is completely depleted and causing starvation damage.</summary>
    public bool IsStarving => _currentHunger <= 0f;

    /// <summary>Raised when the player switches the active hotbar slot.</summary>
    public event Action<int>? OnHotbarSelectionChanged;

    /// <summary>Raised when the equipped weapon changes.</summary>
    public event Action<IWeapon?>? OnWeaponEquipped;

    /// <summary>Raised when player hunger changes appreciably.</summary>
    public event Action<float, float>? OnHungerChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="Player"/> class.
    /// </summary>
    public Player(
        string id,
        Vector2D spawnPosition,
        int maxHp = 100,
        float moveSpeed = 140f,
        int hotbarCapacity = DEFAULT_HOTBAR_CAPACITY,
        Inventory? inventory = null,
        float maxHunger = DEFAULT_MAX_HUNGER,
        float hungerDrainPerSecond = DEFAULT_HUNGER_DRAIN_PER_SEC,
        float starvationInterval = DEFAULT_STARVATION_INTERVAL,
        int starvationDamage = DEFAULT_STARVATION_DAMAGE,
        float minHungerSpeedMultiplier = 0.52f)
        : base(id, spawnPosition, new Vector2D(24f, 24f), maxHp, moveSpeed)
    {
        if (hotbarCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hotbarCapacity), "Hotbar capacity must be positive.");
        }

        if (maxHunger <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHunger), "Maximum hunger must be positive.");
        }

        if (hungerDrainPerSecond < 0f || starvationInterval <= 0f || starvationDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hungerDrainPerSecond), "Invalid hunger or starvation tuning parameter.");
        }

        _inventory = inventory ?? new Inventory(DEFAULT_INVENTORY_CAPACITY);
        _hotbarCapacity = hotbarCapacity;
        _selectedHotbarIndex = 0;
        _equippedToolType = ToolType.None;
        _harvestPower = DEFAULT_BARE_HAND_HARVEST_POWER;

        _maxHunger = maxHunger;
        _currentHunger = maxHunger;
        _hungerDrainPerSecond = hungerDrainPerSecond;
        _starvationInterval = starvationInterval;
        _starvationDamage = starvationDamage;
        _minHungerSpeedMultiplier = global::System.Math.Clamp(minHungerSpeedMultiplier, 0.25f, 1f);
        _lastNotifiedHungerInt = (int)MathF.Round(_currentHunger);

        _buffs.OnBuffsChanged += RebuildEffectiveWeapon;
    }

    /// <summary>Equips a weapon implementing <see cref="IWeapon"/> (Association).</summary>
    public void EquipWeapon(IWeapon? weapon)
    {
        _equippedWeapon = weapon;
        RebuildEffectiveWeapon();
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

    /// <summary>Gets the item stack currently selected on the hotbar, or <see langword="null"/> if empty.</summary>
    public ItemStack? SelectedItemStack => _inventory.GetStackAt(_selectedHotbarIndex);

    /// <summary>Selects a valid 0-based hotbar slot index and equips its item polymorphically.</summary>
    public void SelectHotbarSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _hotbarCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Hotbar slot index is out of range.");
        }

        _selectedHotbarIndex = slotIndex;
        SyncEquippedFromSelectedSlot();
        OnHotbarSelectionChanged?.Invoke(_selectedHotbarIndex);
    }

    /// <summary>Synchronizes equipped weapon/tool state from the currently selected hotbar slot.</summary>
    public void SyncEquippedFromSelectedSlot()
    {
        ItemStack? selected = SelectedItemStack;
        if (selected is null)
        {
            EquipWeapon(null);
            UnequipTool();
            return;
        }

        selected.Item.ApplyHotbarSelection(this);
    }

    /// <summary>Starts the attack or tool swing cooldown using the equipped weapon or default timing.</summary>
    public void TriggerActionCooldown(float? customCooldown = null)
    {
        float cooldown = customCooldown ?? _equippedWeapon?.ComputeCooldown() ?? DEFAULT_BARE_HAND_COOLDOWN;
        _attackCooldownRemaining = global::System.Math.Max(0f, cooldown);
    }

    /// <summary>Restores hunger points clamped at <see cref="MaxHunger"/>.</summary>
    public void RestoreHunger(float amount)
    {
        if (amount < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Hunger restore amount cannot be negative.");
        }

        if (!IsAlive || amount == 0f)
        {
            return;
        }

        _currentHunger = global::System.Math.Min(_maxHunger, _currentHunger + amount);
        if (_currentHunger > 0f)
        {
            _starvationTimer = 0f;
        }

        NotifyHungerIfChanged(force: true);
    }

    /// <summary>Restores exact hunger state from persisted snapshot data.</summary>
    public void RestoreHungerExact(float currentHunger, float maxHunger)
    {
        if (maxHunger > 0f)
        {
            _maxHunger = maxHunger;
        }

        _currentHunger = global::System.Math.Clamp(currentHunger, 0f, _maxHunger);
        _starvationTimer = 0f;
        NotifyHungerIfChanged(force: true);
    }

    /// <summary>Assigns the damage mitigation strategy applied by <see cref="TakeDamage"/>.</summary>
    public void SetDamageMitigationStrategy(IDamageMitigationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _damageMitigationStrategy = strategy;
    }

    /// <summary>Equips an armor piece into its slot, returning any displaced piece to inventory safely.</summary>
    public bool TryEquipArmor(ArmorItem armor)
    {
        ArgumentNullException.ThrowIfNull(armor);
        if (!IsAlive)
        {
            return false;
        }

        _equipment.Equip(armor, out ArmorItem? previous);
        if (previous is not null && !_inventory.TryAddItem(previous, 1))
        {
            _equipment.Equip(previous, out _);
            return false;
        }

        return true;
    }

    /// <summary>Unequips armor from <paramref name="slot"/> back into inventory if space permits.</summary>
    public bool TryUnequipArmor(ArmorSlot slot)
    {
        ArmorItem? current = _equipment.GetSlot(slot);
        if (current is null)
        {
            return false;
        }

        if (!_inventory.CanAddItem(current, 1))
        {
            return false;
        }

        _equipment.Unequip(slot);
        _inventory.TryAddItem(current, 1);
        return true;
    }

    /// <summary>Directly restores equipped armor slots during save-state loading.</summary>
    public void RestoreEquippedArmor(ArmorItem? head, ArmorItem? body, ArmorItem? legs) =>
        _equipment.Restore(head, body, legs);

    /// <inheritdoc />
    public override void TakeDamage(int amount)
    {
        if (amount < 0)
        {
            base.TakeDamage(amount);
            return;
        }

        if (amount == 0 || !IsAlive)
        {
            return;
        }

        int finalDamage = _damageMitigationStrategy.ComputeFinalDamage(amount, TotalDefense);
        if (finalDamage <= 0)
        {
            return;
        }

        base.TakeDamage(finalDamage);
    }

    /// <summary>Applies direct damage bypassing armor mitigation (e.g. starvation) while respecting active difficulty rules.</summary>
    public void TakeDirectDamage(int amount)
    {
        if (amount < 0)
        {
            base.TakeDamage(amount);
            return;
        }

        if (amount == 0 || !IsAlive)
        {
            return;
        }

        int finalDamage = _damageMitigationStrategy.ComputeDirectDamage(amount);
        if (finalDamage <= 0)
        {
            return;
        }

        base.TakeDamage(finalDamage);
    }

    /// <summary>Applies a temporary <see cref="IFoodBuffStrategy"/> to this player.</summary>
    public void ApplyBuff(IFoodBuffStrategy buffStrategy)
    {
        ArgumentNullException.ThrowIfNull(buffStrategy);
        if (IsAlive)
        {
            _buffs.ApplyBuff(buffStrategy);
        }
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

        if (!IsAlive)
        {
            return;
        }

        _buffs.Update(deltaTime, this);

        if (_hungerDrainPerSecond > 0f && deltaTime > 0f)
        {
            _currentHunger = global::System.Math.Max(0f, _currentHunger - (_hungerDrainPerSecond * deltaTime));
            NotifyHungerIfChanged();

            if (_currentHunger <= 0f && _starvationDamage > 0)
            {
                _starvationTimer += deltaTime;
                if (_starvationTimer >= _starvationInterval)
                {
                    _starvationTimer -= _starvationInterval;
                    TakeDirectDamage(_starvationDamage);
                }
            }
            else
            {
                _starvationTimer = 0f;
            }
        }
    }

    private void RebuildEffectiveWeapon()
    {
        if (_equippedWeapon is null)
        {
            _effectiveEquippedWeapon = null;
            return;
        }

        int bonus = _buffs.BonusDamage;
        _effectiveEquippedWeapon = bonus > 0
            ? new FoodDamageBuffDecorator(_equippedWeapon, bonus)
            : _equippedWeapon;
    }

    private void NotifyHungerIfChanged(bool force = false)
    {
        int rounded = (int)MathF.Round(_currentHunger);
        if (force || rounded != _lastNotifiedHungerInt)
        {
            _lastNotifiedHungerInt = rounded;
            OnHungerChanged?.Invoke(_currentHunger, _maxHunger);
        }
    }
}
