using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a player-built world structure such as a wooden wall, stone wall, workbench, or torch.
/// </summary>
public sealed class Structure : Entity, IDamageable
{
    private readonly StructureType _structureType;
    private readonly bool _isSolid;
    private readonly bool _isCraftingStation;
    private readonly float _lightRadius;
    private readonly int _tileCol;
    private readonly int _tileRow;
    private readonly int _maxHp;
    private int _currentHp;

    /// <summary>Gets the structure archetype.</summary>
    public StructureType StructureType => _structureType;

    /// <summary>Gets a value indicating whether this structure blocks movement.</summary>
    public bool IsSolid => _isSolid;

    /// <summary>Gets a value indicating whether this structure enables workbench crafting recipes.</summary>
    public bool IsCraftingStation => _isCraftingStation;

    /// <summary>Gets the illumination radius in world units (positive for torches).</summary>
    public float LightRadius => _lightRadius;

    /// <summary>Gets the grid column occupied by this structure.</summary>
    public int TileCol => _tileCol;

    /// <summary>Gets the grid row occupied by this structure.</summary>
    public int TileRow => _tileRow;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="Structure"/> class.
    /// </summary>
    public Structure(
        string id,
        StructureType structureType,
        Vector2D position,
        int tileCol,
        int tileRow,
        int maxHp,
        bool isSolid,
        bool isCraftingStation = false,
        float lightRadius = 0f,
        Vector2D? size = null)
        : base(id, position, size ?? new Vector2D(32f, 32f))
    {
        if (maxHp <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Structure max HP must be positive.");
        }

        if (lightRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(lightRadius), "Light radius cannot be negative.");
        }

        _structureType = structureType;
        _tileCol = tileCol;
        _tileRow = tileRow;
        _maxHp = maxHp;
        _currentHp = maxHp;
        _isSolid = isSolid;
        _isCraftingStation = isCraftingStation;
        _lightRadius = lightRadius;
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
            OnDefeated?.Invoke(this);
        }
    }

    /// <inheritdoc />
    public void Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Repair amount cannot be negative.");
        }

        if (amount == 0 || !IsAlive || _currentHp == _maxHp)
        {
            return;
        }

        _currentHp = global::System.Math.Min(_maxHp, _currentHp + amount);
        OnHealthChanged?.Invoke(_currentHp, _maxHp);
    }

    /// <summary>Restores exact structure health within [0, MaxHp] during save-state loading.</summary>
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
        // Placed structures are stationary.
    }
}
