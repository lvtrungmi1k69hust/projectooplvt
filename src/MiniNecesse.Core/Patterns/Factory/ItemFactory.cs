using System;
using System.Collections.Generic;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Farming;
using MiniNecesse.Core.Rules;

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
            [ItemCategory.Structure] = CreateStructureItem,
            [ItemCategory.Seed] = CreateSeedItem
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
            new ItemBlueprint("coal", "Coal", ItemCategory.Material, 99, "Combustible coal mined from dark seams."),
            new ItemBlueprint("crystal", "Arcane Crystal", ItemCategory.Material, 99, "Luminous crystal shard pulsing with energy."),
            new ItemBlueprint("wheat", "Wheat", ItemCategory.Material, 99, "Golden grain harvested from crops."),
            new ItemBlueprint("carrot", "Fresh Carrot", ItemCategory.Consumable, 30, "Crisp garden carrot restoring 6 HP and 14 Hunger.", HealAmount: 6, HungerRestore: 14f),
            new ItemBlueprint("pumpkin", "Harvest Pumpkin", ItemCategory.Consumable, 20, "Plump golden pumpkin restoring 10 HP and 20 Hunger.", HealAmount: 10, HungerRestore: 20f),
            new ItemBlueprint("wool", "Wool", ItemCategory.Material, 99, "Soft fleece sheared from sheep."),
            new ItemBlueprint("feather", "Feather", ItemCategory.Material, 99, "Light plumage ideal for fletching arrows."),
            new ItemBlueprint("leather", "Leather", ItemCategory.Material, 99, "Tough hide harvested from cattle."),
            new ItemBlueprint("slime_gel", "Slime Gel", ItemCategory.Material, 99, "Gelatinous essence dropped by slimes."),
            new ItemBlueprint("wood_axe", "Wood Axe", ItemCategory.Tool, 1, "Basic axe for chopping trees.", ToolType: ToolType.Axe, HarvestPower: 10, HarvestRange: 48f, BaseCooldown: 0.35f),
            new ItemBlueprint("wood_hoe", "Wood Hoe", ItemCategory.Tool, 1, "Tills grass or sand into fertile farm plots.", ToolType: ToolType.Hoe, HarvestPower: 8, HarvestRange: 56f, BaseCooldown: 0.30f),
            new ItemBlueprint("wood_pickaxe", "Wood Pickaxe", ItemCategory.Tool, 1, "Basic pickaxe for mining stone.", ToolType: ToolType.Pickaxe, HarvestPower: 10, HarvestRange: 48f, BaseCooldown: 0.38f),
            new ItemBlueprint("stone_pickaxe", "Stone Pickaxe", ItemCategory.Tool, 1, "Upgraded pickaxe for mining ore veins.", ToolType: ToolType.Pickaxe, HarvestPower: 18, HarvestRange: 50f, BaseCooldown: 0.34f),
            new ItemBlueprint("copper_pickaxe", "Copper Pickaxe", ItemCategory.Tool, 1, "Durable copper pickaxe for iron and gold veins.", ToolType: ToolType.Pickaxe, HarvestPower: 26, HarvestRange: 52f, BaseCooldown: 0.30f),
            new ItemBlueprint("copper_sword", "Copper Sword", ItemCategory.Weapon, 1, "Balanced melee blade.", BaseDamage: 22, AttackRange: 46f, BaseCooldown: 0.32f),
            new ItemBlueprint("iron_sword", "Iron Sword", ItemCategory.Weapon, 1, "Forged iron blade with superior damage.", BaseDamage: 36, AttackRange: 50f, BaseCooldown: 0.28f),
            new ItemBlueprint("gold_greatsword", "Gold Greatsword", ItemCategory.Weapon, 1, "Massive heavy golden greatsword with huge cleave reach.", BaseDamage: 54, AttackRange: 64f, BaseCooldown: 0.42f),
            new ItemBlueprint("wooden_bow", "Wooden Bow", ItemCategory.Weapon, 1, "Ranged bow firing arrows.", BaseDamage: 16, AttackRange: 260f, BaseCooldown: 0.45f, IsRanged: true, ProjectileSpeed: 340f),
            new ItemBlueprint("hunter_bow", "Hunter Bow", ItemCategory.Weapon, 1, "Feather-fletched longbow with high velocity.", BaseDamage: 30, AttackRange: 300f, BaseCooldown: 0.36f, IsRanged: true, ProjectileSpeed: 410f),
            new ItemBlueprint("fire_staff", "Fire Staff", ItemCategory.Weapon, 1, "Arcane staff launching rapid blazing bolts.", BaseDamage: 28, AttackRange: 300f, BaseCooldown: 0.34f, IsRanged: true, ProjectileSpeed: 390f),
            new ItemBlueprint("crystal_staff", "Crystal Staff", ItemCategory.Weapon, 1, "Focuses arcane crystals into devastating bolts.", BaseDamage: 48, AttackRange: 330f, BaseCooldown: 0.30f, IsRanged: true, ProjectileSpeed: 450f),
            new ItemBlueprint("iron_crossbow", "Iron Crossbow", ItemCategory.Weapon, 1, "Heavy armor-piercing crossbow with high bolt velocity.", BaseDamage: 42, AttackRange: 340f, BaseCooldown: 0.55f, IsRanged: true, ProjectileSpeed: 460f),
            new ItemBlueprint("berry", "Wild Berry", ItemCategory.Consumable, 30, "Sweet forest berries restoring 8 HP and 12 Hunger.", HealAmount: 8, HungerRestore: 12f),
            new ItemBlueprint("bread", "Fresh Bread", ItemCategory.Consumable, 20, "Baked wheat loaf (+15 HP, +35 Hunger, +15% Speed 20s).", HealAmount: 15, HungerRestore: 35f, BuffStrategy: new StatFoodBuffStrategy("well_fed_speed", "Well Fed (Speed)", 20f, speedMultiplier: 1.15f)),
            new ItemBlueprint("raw_meat", "Raw Meat", ItemCategory.Consumable, 20, "Restores 5 HP and 15 Hunger.", HealAmount: 5, HungerRestore: 15f),
            new ItemBlueprint("cooked_meat", "Cooked Roast", ItemCategory.Consumable, 20, "Hearty seared roast (+30 HP, +55 Hunger, +8 Damage 25s).", HealAmount: 30, HungerRestore: 55f, BuffStrategy: new StatFoodBuffStrategy("hearty_strength", "Hearty Strength", 25f, bonusDamage: 8)),
            new ItemBlueprint("carrot_stew", "Carrot Stew", ItemCategory.Consumable, 15, "Warm stew (+25 HP, +65 Hunger, Speed & Regen 30s).", HealAmount: 25, HungerRestore: 65f, BuffStrategy: new StatFoodBuffStrategy("swift_stride", "Swift Stride", 30f, speedMultiplier: 1.22f, regenPerSecond: 2f)),
            new ItemBlueprint("pumpkin_pie", "Pumpkin Pie", ItemCategory.Consumable, 15, "Spiced festive pie (+35 HP, +75 Hunger, +12 Damage & Speed 40s).", HealAmount: 35, HungerRestore: 75f, BuffStrategy: new StatFoodBuffStrategy("warrior_feast", "Warrior Feast", 40f, speedMultiplier: 1.15f, bonusDamage: 12)),
            new ItemBlueprint("golden_bread", "Golden Bread", ItemCategory.Consumable, 15, "Artisanal bread (+40 HP, +80 Hunger, +6 Damage & +4 Def 35s).", HealAmount: 40, HungerRestore: 80f, BuffStrategy: new StatFoodBuffStrategy("iron_stomach", "Iron Stomach", 35f, bonusDamage: 6, bonusDefense: 4)),
            new ItemBlueprint("wheat_seed", "Wheat Seeds", ItemCategory.Seed, 50, "Plant on tilled farmland to grow Golden Wheat.", CropId: "wheat_crop"),
            new ItemBlueprint("carrot_seed", "Carrot Seeds", ItemCategory.Seed, 50, "Plant on tilled farmland to grow Sweet Carrots.", CropId: "carrot_crop"),
            new ItemBlueprint("pumpkin_seed", "Pumpkin Seeds", ItemCategory.Seed, 50, "Plant on tilled farmland to grow Giant Pumpkins.", CropId: "pumpkin_crop"),
            new ItemBlueprint("health_potion", "Health Potion", ItemCategory.Consumable, 20, "Restores 40 HP instantly.", HealAmount: 40),
            new ItemBlueprint("workbench", "Workbench", ItemCategory.Structure, 10, "Unlocks advanced crafting recipes nearby.", StructureType: StructureType.Workbench),
            new ItemBlueprint("wooden_wall", "Wooden Wall", ItemCategory.Structure, 50, "Solid wooden defensive barrier.", StructureType: StructureType.WoodenWall),
            new ItemBlueprint("stone_wall", "Stone Wall", ItemCategory.Structure, 50, "Reinforced stone defensive barrier.", StructureType: StructureType.StoneWall),
            new ItemBlueprint("torch", "Torch", ItemCategory.Structure, 50, "Illuminates nearby ground at night.", StructureType: StructureType.Torch),
            new ItemBlueprint("wooden_fence", "Wooden Fence", ItemCategory.Structure, 50, "Solid wooden fence for pens and perimeters.", StructureType: StructureType.WoodenFence)
        });

        return factory;
    }

    /// <summary>Registers food definitions loaded from JSON, updating defaults cleanly.</summary>
    public void RegisterFoodDefinitions(IEnumerable<FoodDefinition> foods)
    {
        ArgumentNullException.ThrowIfNull(foods);
        foreach (FoodDefinition food in foods)
        {
            RegisterBlueprint(new ItemBlueprint(
                food.Id,
                food.Name,
                ItemCategory.Consumable,
                food.MaxStackSize,
                food.Description,
                HealAmount: food.HealAmount,
                HungerRestore: food.HungerRestore,
                BuffStrategy: food.BuffStrategy));
        }
    }

    /// <summary>Registers seed items corresponding to crop definitions loaded from JSON.</summary>
    public void RegisterCropDefinitions(IEnumerable<CropDefinition> crops)
    {
        ArgumentNullException.ThrowIfNull(crops);
        foreach (CropDefinition crop in crops)
        {
            RegisterBlueprint(new ItemBlueprint(
                crop.SeedItemId,
                crop.SeedName,
                ItemCategory.Seed,
                50,
                $"Plant on tilled soil to grow {crop.DisplayName}.",
                CropId: crop.CropId));
        }
    }

    private static Item CreateMaterialItem(ItemBlueprint bp) =>
        new MaterialItem(bp.Id, bp.Name, bp.MaxStackSize, bp.Description);

    private static Item CreateToolItem(ItemBlueprint bp) =>
        new ToolItem(bp.Id, bp.Name, bp.ToolType, bp.HarvestPower, bp.HarvestRange, bp.BaseCooldown, bp.Description);

    private static Item CreateWeaponItem(ItemBlueprint bp) =>
        new WeaponItem(bp.Id, bp.Name, bp.BaseDamage, bp.AttackRange, bp.BaseCooldown, bp.IsRanged, bp.ProjectileSpeed, bp.Description);

    private static Item CreateConsumableItem(ItemBlueprint bp) =>
        new ConsumableItem(bp.Id, bp.Name, bp.HealAmount, bp.MaxStackSize, bp.Description, bp.HungerRestore, bp.BuffStrategy);

    private static Item CreateStructureItem(ItemBlueprint bp) =>
        new StructureItem(bp.Id, bp.Name, bp.StructureType, bp.MaxStackSize, bp.Description);

    private static Item CreateSeedItem(ItemBlueprint bp) =>
        new SeedItem(bp.Id, bp.Name, bp.CropId ?? bp.Id, bp.MaxStackSize, bp.Description);
}
