using System;
using System.Collections.Generic;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Patterns.Factory;

/// <summary>
/// GoF Factory Method (Concrete Creator): constructs polymorphic <see cref="Item"/> instances
/// from data-driven <see cref="ItemBlueprint"/> records using a category creator registry
/// (zero hardcoded switch-case statements).
/// </summary>
public sealed class ItemFactory
{
    private readonly Dictionary<ItemCategory, Func<ItemBlueprint, Item>> _categoryCreators;
    private readonly Dictionary<string, ItemBlueprint> _blueprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Item> _sharedItemCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets total registered item blueprints.</summary>
    public int RegisteredCount => _blueprints.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemFactory"/> class with extensible category creators.
    /// </summary>
    public ItemFactory()
    {
        _categoryCreators = new Dictionary<ItemCategory, Func<ItemBlueprint, Item>>
        {
            [ItemCategory.Material] = CreateMaterialItem,
            [ItemCategory.Tool] = CreateToolItem,
            [ItemCategory.Weapon] = CreateWeaponItem,
            [ItemCategory.Consumable] = CreateConsumableItem,
            [ItemCategory.Structure] = CreateStructureItem
        };
    }

    /// <summary>Registers or replaces a category creator delegate (OCP extension point).</summary>
    public void RegisterCategoryCreator(ItemCategory category, Func<ItemBlueprint, Item> creator)
    {
        ArgumentNullException.ThrowIfNull(creator);
        _categoryCreators[category] = creator;
    }

    /// <summary>Registers an item blueprint loaded from external configuration.</summary>
    public void RegisterBlueprint(ItemBlueprint blueprint)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        _blueprints[blueprint.Id] = blueprint;
        _sharedItemCache.Remove(blueprint.Id);
    }

    /// <summary>Registers a batch of item blueprints.</summary>
    public void RegisterBlueprints(IEnumerable<ItemBlueprint> blueprints)
    {
        ArgumentNullException.ThrowIfNull(blueprints);
        foreach (ItemBlueprint blueprint in blueprints)
        {
            RegisterBlueprint(blueprint);
        }
    }

    /// <summary>Creates a fresh <see cref="Item"/> instance for <paramref name="itemId"/>.</summary>
    public Item CreateItem(string itemId)
    {
        if (!_blueprints.TryGetValue(itemId, out ItemBlueprint? blueprint))
        {
            throw new KeyNotFoundException($"Item blueprint '{itemId}' is not registered in ItemFactory.");
        }

        if (!_categoryCreators.TryGetValue(blueprint.Category, out Func<ItemBlueprint, Item>? creator))
        {
            throw new InvalidOperationException($"No creator registered for item category '{blueprint.Category}'.");
        }

        return creator(blueprint);
    }

    /// <summary>Returns a shared cached <see cref="Item"/> instance (Flyweight-style lookup) for <paramref name="itemId"/>.</summary>
    public Item ResolveItem(string itemId)
    {
        if (_sharedItemCache.TryGetValue(itemId, out Item? cached))
        {
            return cached;
        }

        Item created = CreateItem(itemId);
        _sharedItemCache[itemId] = created;
        return created;
    }

    /// <summary>Checks whether an item blueprint is registered.</summary>
    public bool ContainsBlueprint(string itemId) => _blueprints.ContainsKey(itemId);

    /// <summary>Creates an <see cref="ItemFactory"/> pre-populated with standard MiniNecesse item blueprints.</summary>
    public static ItemFactory CreateDefault()
    {
        var factory = new ItemFactory();
        factory.RegisterBlueprints(new[]
        {
            new ItemBlueprint("wood", "Wood", ItemCategory.Material, 99, "Sturdy timber chopped from trees."),
            new ItemBlueprint("stone", "Stone", ItemCategory.Material, 99, "Raw stone mined from island boulders."),
            new ItemBlueprint("copper_ore", "Copper Ore", ItemCategory.Material, 99, "Unrefined copper ore."),
            new ItemBlueprint("iron_ore", "Iron Ore", ItemCategory.Material, 99, "Dense iron ore."),
            new ItemBlueprint("gold_ore", "Gold Ore", ItemCategory.Material, 99, "Precious gold ore."),
            new ItemBlueprint("slime_gel", "Slime Gel", ItemCategory.Material, 99, "Gelatinous essence dropped by slimes."),
            new ItemBlueprint("wood_axe", "Wood Axe", ItemCategory.Tool, 1, "Basic axe for chopping trees.", ToolType: ToolType.Axe, HarvestPower: 10, HarvestRange: 48f, BaseCooldown: 0.35f),
            new ItemBlueprint("wood_pickaxe", "Wood Pickaxe", ItemCategory.Tool, 1, "Basic pickaxe for mining stone.", ToolType: ToolType.Pickaxe, HarvestPower: 10, HarvestRange: 48f, BaseCooldown: 0.38f),
            new ItemBlueprint("stone_pickaxe", "Stone Pickaxe", ItemCategory.Tool, 1, "Upgraded pickaxe for mining ore veins.", ToolType: ToolType.Pickaxe, HarvestPower: 18, HarvestRange: 50f, BaseCooldown: 0.34f),
            new ItemBlueprint("copper_pickaxe", "Copper Pickaxe", ItemCategory.Tool, 1, "Durable copper pickaxe for iron and gold veins.", ToolType: ToolType.Pickaxe, HarvestPower: 26, HarvestRange: 52f, BaseCooldown: 0.30f),
            new ItemBlueprint("copper_sword", "Copper Sword", ItemCategory.Weapon, 1, "Balanced melee blade.", BaseDamage: 22, AttackRange: 46f, BaseCooldown: 0.32f),
            new ItemBlueprint("iron_sword", "Iron Sword", ItemCategory.Weapon, 1, "Forged iron blade with superior damage.", BaseDamage: 36, AttackRange: 50f, BaseCooldown: 0.28f),
            new ItemBlueprint("wooden_bow", "Wooden Bow", ItemCategory.Weapon, 1, "Ranged bow firing arrows.", BaseDamage: 16, AttackRange: 260f, BaseCooldown: 0.45f, IsRanged: true, ProjectileSpeed: 340f),
            new ItemBlueprint("health_potion", "Health Potion", ItemCategory.Consumable, 20, "Restores 40 HP instantly.", HealAmount: 40),
            new ItemBlueprint("workbench", "Workbench", ItemCategory.Structure, 10, "Unlocks advanced crafting recipes nearby.", StructureType: StructureType.Workbench),
            new ItemBlueprint("wooden_wall", "Wooden Wall", ItemCategory.Structure, 50, "Solid wooden defensive barrier.", StructureType: StructureType.WoodenWall),
            new ItemBlueprint("stone_wall", "Stone Wall", ItemCategory.Structure, 50, "Reinforced stone defensive barrier.", StructureType: StructureType.StoneWall),
            new ItemBlueprint("torch", "Torch", ItemCategory.Structure, 50, "Illuminates nearby ground at night.", StructureType: StructureType.Torch)
        });

        return factory;
    }

    private static Item CreateMaterialItem(ItemBlueprint bp) =>
        new MaterialItem(bp.Id, bp.Name, bp.MaxStackSize, bp.Description);

    private static Item CreateToolItem(ItemBlueprint bp) =>
        new ToolItem(bp.Id, bp.Name, bp.ToolType, bp.HarvestPower, bp.HarvestRange, bp.BaseCooldown, bp.Description);

    private static Item CreateWeaponItem(ItemBlueprint bp) =>
        new WeaponItem(bp.Id, bp.Name, bp.BaseDamage, bp.AttackRange, bp.BaseCooldown, bp.IsRanged, bp.ProjectileSpeed, bp.Description);

    private static Item CreateConsumableItem(ItemBlueprint bp) =>
        new ConsumableItem(bp.Id, bp.Name, bp.HealAmount, bp.MaxStackSize, bp.Description);

    private static Item CreateStructureItem(ItemBlueprint bp) =>
        new StructureItem(bp.Id, bp.Name, bp.StructureType, bp.MaxStackSize, bp.Description);
}
