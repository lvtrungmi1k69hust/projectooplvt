using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a melee or ranged combat weapon item implementing <see cref="IWeapon"/>.
/// </summary>
public sealed class WeaponItem : Item, IWeapon
{
    private readonly int _baseDamage;
    private readonly float _attackRange;
    private readonly float _baseCooldown;
    private readonly bool _isRanged;
    private readonly float _projectileSpeed;

    /// <inheritdoc />
    public string WeaponId => Id;

    /// <inheritdoc />
    public int BaseDamage => _baseDamage;

    /// <inheritdoc />
    public float AttackRange => _attackRange;

    /// <inheritdoc />
    public float BaseCooldown => _baseCooldown;

    /// <inheritdoc />
    public bool IsRanged => _isRanged;

    /// <inheritdoc />
    public float ProjectileSpeed => _projectileSpeed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeaponItem"/> class.
    /// </summary>
    public WeaponItem(
        string id,
        string name,
        int baseDamage,
        float attackRange,
        float baseCooldown,
        bool isRanged = false,
        float projectileSpeed = 0f,
        string description = "")
        : base(id, name, ItemCategory.Weapon, maxStackSize: 1, description)
    {
        if (baseDamage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDamage), "Weapon damage must be positive.");
        }

        if (attackRange <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(attackRange), "Attack range must be positive.");
        }

        if (baseCooldown <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(baseCooldown), "Attack cooldown must be positive.");
        }

        if (isRanged && projectileSpeed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(projectileSpeed), "Ranged weapons require positive projectile speed.");
        }

        _baseDamage = baseDamage;
        _attackRange = attackRange;
        _baseCooldown = baseCooldown;
        _isRanged = isRanged;
        _projectileSpeed = isRanged ? projectileSpeed : 0f;
    }

    /// <inheritdoc />
    public int ComputeDamage() => _baseDamage;

    /// <inheritdoc />
    public float ComputeCooldown() => _baseCooldown;

    /// <inheritdoc />
    public override void ApplyHotbarSelection(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.EquipWeapon(this);
        player.UnequipTool();
    }
}
