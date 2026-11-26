using System;
using System.Collections.Generic;
using System.IO;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class GameplayMechanicsExpansionTests
{
    [Fact]
    public void CampaignDayLimit_AutoSummonsBossOnlyOnFinalNight_WhileEndlessStaysUnlimited()
    {
        var loader = new JsonLevelLoader();
        GameConfig config = loader.LoadConfig();
        LoadedIslandPackage island1 = loader.LoadIsland("ISLAND_01");
        LoadedIslandPackage island5 = loader.LoadIsland("ISLAND_05");
        DifficultyCatalog diffCatalog = loader.LoadDifficultyCatalog();
        EndlessBiomeCatalog biomeCatalog = loader.LoadEndlessBiomeCatalog();

        Assert.Equal(3, island1.CampaignMaxDays);
        Assert.Equal(5, island5.CampaignMaxDays);

        var items = ItemFactory.CreateDefault();
        var level = new IslandLevel("ISLAND_01", "Campaign Test", "Forest", "KingSlime", 32, 32, 16, 16);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition, config.PlayerBaseHp, config.PlayerBaseSpeed);
        var dayNight = new DayNightCycle(dayDurationSeconds: 10f, nightDurationSeconds: 10f);
        var cues = new List<string>();

        using var campaignSession = new GameSession(
            level,
            player,
            items.ResolveItem,
            dayNight,
            difficulty: Difficulty.Normal,
            gameMode: GameMode.Campaign,
            difficultyCatalog: diffCatalog);
        campaignSession.OnSoundEffectRequested += cues.Add;

        EnemyFactory enemyFactory = EnemyFactory.CreateDefault(difficultyCatalog: diffCatalog);
        campaignSession.ConfigureCampaignAutoBoss(island1.Boss, enemyFactory, island1.CampaignMaxDays);
        campaignSession.Start();

        Assert.Equal(3, campaignSession.CampaignMaxDays);
        Assert.Null(campaignSession.ActiveBoss);

        // Day 1 -> Night 1 -> Day 2 -> Night 2 -> Day 3: Boss should NOT auto-summon before final night.
        for (int step = 0; step < 42; step++)
        {
            campaignSession.Update(1.0f);
        }

        Assert.Equal(3, dayNight.DayNumber);
        Assert.Equal(DayPhase.Day, dayNight.CurrentPhase);
        Assert.Null(campaignSession.ActiveBoss);

        // Crossing into Day 3 Night auto-summons the Campaign Boss.
        for (int step = 0; step < 10; step++)
        {
            campaignSession.Update(1.0f);
        }

        Assert.Equal(3, dayNight.DayNumber);
        Assert.Equal(DayPhase.Night, dayNight.CurrentPhase);
        Assert.NotNull(campaignSession.ActiveBoss);
        Assert.Contains("boss_spawn", cues);

        // Endless mode has no day cap (CampaignMaxDays == 0) and never auto-summons the boss by day cap.
        var endlessGenerator = new SeededEndlessLevelGenerator(biomeCatalog);
        LoadedIslandPackage endlessPkg = endlessGenerator.GenerateWorld(424242);
        Assert.Equal(0, endlessPkg.CampaignMaxDays);

        var endlessCycle = new DayNightCycle(10f, 10f);
        var endlessPlayer = new Player("player_e", endlessPkg.Level.PlayerSpawnWorldPosition);
        using var endlessSession = new GameSession(
            endlessPkg.Level,
            endlessPlayer,
            items.ResolveItem,
            endlessCycle,
            difficulty: Difficulty.Normal,
            gameMode: GameMode.Endless,
            worldSeed: 424242,
            difficultyCatalog: diffCatalog);
        endlessSession.Start();

        for (int i = 0; i < 85; i++)
        {
            endlessCycle.Advance(1.0f);
        }

        Assert.Equal(5, endlessCycle.DayNumber);
        Assert.Equal(0, endlessSession.CampaignMaxDays);
        Assert.Null(endlessSession.ActiveBoss);
    }

    [Fact]
    public void HungerScaledSpeed_OutrunsBossAtFull_MatchesNearHalf_AndDropsBelowWhenStarving()
    {
        var loader = new JsonLevelLoader();
        GameConfig config = loader.LoadConfig();
        LoadedIslandPackage island1 = loader.LoadIsland("ISLAND_01");
        LoadedIslandPackage island5 = loader.LoadIsland("ISLAND_05");
        IReadOnlyList<FoodDefinition> foods = loader.LoadFoods();

        var itemFactory = ItemFactory.CreateDefault();
        itemFactory.RegisterFoodDefinitions(foods);

        Assert.Equal(165f, config.PlayerBaseSpeed);
        Assert.InRange(island1.Boss.MoveSpeed, 118f, 130f);
        Assert.InRange(island5.Boss.MoveSpeed, 120f, 132f);

        var player = new Player(
            "player_speed",
            new Vector2D(64f, 64f),
            config.PlayerBaseHp,
            config.PlayerBaseSpeed,
            config.HotbarSlots,
            Inventory.CreateUnlimited(),
            config.PlayerMaxHunger,
            hungerDrainPerSecond: 0f,
            minHungerSpeedMultiplier: config.MinHungerSpeedMultiplier);

        // 100/100 hunger -> faster than every campaign boss (165 > 128).
        Assert.Equal(165f, player.EffectiveMoveSpeed, precision: 2);
        Assert.True(player.EffectiveMoveSpeed > island5.Boss.MoveSpeed);

        // 50/100 hunger -> ~125.4 speed (right around campaign Boss speeds 120..128).
        player.RestoreHungerExact(50f, 100f);
        Assert.InRange(player.EffectiveMoveSpeed, 122f, 128f);

        // Below 50/100 hunger (e.g. 20/100 and 0/100) -> noticeably slower than Boss.
        player.RestoreHungerExact(20f, 100f);
        Assert.True(player.EffectiveMoveSpeed < island1.Boss.MoveSpeed);

        player.RestoreHungerExact(0f, 100f);
        Assert.Equal(config.PlayerBaseSpeed * config.MinHungerSpeedMultiplier, player.EffectiveMoveSpeed, precision: 2);
        Assert.True(player.EffectiveMoveSpeed < island1.Boss.MoveSpeed - 25f);

        // Eating food restores hunger and brings speed back above the Boss immediately.
        Item cookedMeat = itemFactory.ResolveItem("cooked_meat");
        Assert.True(cookedMeat.TryUse(player));
        Assert.True(player.CurrentHunger > 50f);
        Assert.True(player.EffectiveMoveSpeed > island5.Boss.MoveSpeed);
    }

    [Fact]
    public void UnlimitedBackpackAndHotbarReplace_EnforcesWeaponOnlySlotsAndPersistsAcrossSaveLoad()
    {
        var loader = new JsonLevelLoader();
        DifficultyCatalog catalog = loader.LoadDifficultyCatalog();
        var itemFactory = ItemFactory.CreateDefault();
        itemFactory.RegisterFoodDefinitions(loader.LoadFoods());
        itemFactory.RegisterArmorDefinitions(loader.LoadArmorItems());

        Inventory unlimited = Inventory.CreateUnlimited();
        Assert.True(unlimited.IsUnlimited);

        // Unlimited backpack accepts far more than old 20-slot limit cleanly.
        string[] sampleIds =
        {
            "wood", "stone", "copper_ore", "iron_ore", "gold_ore", "coal", "crystal", "wheat",
            "wool", "feather", "leather", "slime_gel", "wood_axe", "wood_hoe", "wood_pickaxe",
            "copper_sword", "iron_sword", "gold_greatsword", "wooden_bow", "hunter_bow",
            "fire_staff", "crystal_staff", "iron_crossbow", "berry", "bread", "raw_meat",
            "cooked_meat", "carrot", "pumpkin", "carrot_stew", "pumpkin_pie", "golden_bread",
            "health_potion", "workbench", "wooden_wall", "stone_wall", "torch", "wooden_fence",
            "wooden_door", "wooden_chest", "copper_helmet", "copper_chestplate", "copper_greaves"
        };

        for (int i = 0; i < sampleIds.Length; i++)
        {
            Assert.True(unlimited.TryAddItem(itemFactory.ResolveItem(sampleIds[i]), 3));
        }

        // Duplicate batch of non-stackable weapons pushes used stack count well above 50.
        for (int extra = 0; extra < 15; extra++)
        {
            Assert.True(unlimited.TryAddItem(itemFactory.ResolveItem("iron_sword"), 1));
        }

        Assert.True(unlimited.UsedSlots > 50);

        var uniqueItems = new List<(Item Item, int TotalQuantity)>();
        unlimited.GetUniqueItems(uniqueItems);
        Assert.Equal(sampleIds.Length, uniqueItems.Count);
        Assert.Equal(18, Assert.Single(uniqueItems, u => u.Item.Id == "iron_sword").TotalQuantity);

        // Hotbar 1-3 strictly Weapons; 4-9 allow Food, Tools, Weapons, Structures, Armor.
        var level = new IslandLevel("ISLAND_01", "Bag Test", "Forest", "KingSlime", 32, 32, 16, 16);
        var player = new Player("player_bag", level.PlayerSpawnWorldPosition, inventory: unlimited);
        player.EnableExplicitHotbar();

        Assert.False(player.TryReplaceHotbarSlot("cooked_meat", 0, out string? foodErr));
        Assert.Contains("Weapons", foodErr);
        Assert.False(player.TryReplaceHotbarSlot("wood_axe", 1, out _));
        Assert.False(player.TryReplaceHotbarSlot("wooden_wall", 2, out _));

        Assert.True(player.TryReplaceHotbarSlot("copper_sword", 0, out _));
        Assert.True(player.TryReplaceHotbarSlot("iron_sword", 1, out _));
        Assert.True(player.TryReplaceHotbarSlot("wood_axe", 3, out _));
        Assert.True(player.TryReplaceHotbarSlot("wooden_bow", 8, out _)); // Stronger weapon also permitted in slots 4-9!
        Assert.True(player.TryReplaceHotbarSlot("cooked_meat", 6, out _));

        player.SelectHotbarSlot(6);
        Assert.NotNull(player.SelectedItemStack);
        Assert.Equal("cooked_meat", player.SelectedItemStack!.Item.Id);

        // Replace swap between two owned items on slots 4..9 preserves swap cleanly.
        Assert.True(player.TryReplaceHotbarSlot("wooden_bow", 6, out _));
        Assert.True(player.TryGetHotbarEntry(6, out Item? slot6After, out _));
        Assert.True(player.TryGetHotbarEntry(8, out Item? slot8After, out _));
        Assert.Equal("wooden_bow", slot6After!.Id);
        Assert.Equal("cooked_meat", slot8After!.Id);

        // Save -> Restore round-trip preserves explicit 9-slot bindings.
        using var session = new GameSession(level, player, itemFactory.ResolveItem, difficulty: Difficulty.Normal, difficultyCatalog: catalog);
        session.Start();

        string tempSavesDir = Path.Combine(Path.GetTempPath(), $"mininecesse_hotbar_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSavesDir);
        try
        {
            var storage = new JsonSaveStorage(tempSavesDir);
            storage.Save(1, session.CreateSnapshot());
            GameStateDTO loaded = storage.Load(1);

            var playerRestored = new Player("player_bag_2", level.PlayerSpawnWorldPosition, inventory: Inventory.CreateUnlimited());
            using var restoredSession = new GameSession(level, playerRestored, itemFactory.ResolveItem, difficulty: Difficulty.Normal, difficultyCatalog: catalog);
            restoredSession.RestoreFromSnapshot(loaded, EnemyFactory.CreateDefault());

            Assert.True(playerRestored.TryGetHotbarEntry(0, out Item? r0, out _));
            Assert.True(playerRestored.TryGetHotbarEntry(6, out Item? r6, out _));
            Assert.True(playerRestored.TryGetHotbarEntry(8, out Item? r8, out _));
            Assert.Equal("copper_sword", r0!.Id);
            Assert.Equal("wooden_bow", r6!.Id);
            Assert.Equal("cooked_meat", r8!.Id);

            // Legacy snapshot (HotbarItemIds == null) migrates smoothly into unlimited explicit hotbar.
            loaded.Player.HotbarItemIds = null;
            var playerLegacy = new Player("player_legacy", level.PlayerSpawnWorldPosition, inventory: Inventory.CreateUnlimited());
            using var legacySession = new GameSession(level, playerLegacy, itemFactory.ResolveItem, difficulty: Difficulty.Normal, difficultyCatalog: catalog);
            legacySession.RestoreFromSnapshot(loaded, EnemyFactory.CreateDefault());
            Assert.True(playerLegacy.UsesExplicitHotbar);
        }
        finally
        {
            if (Directory.Exists(tempSavesDir))
            {
                Directory.Delete(tempSavesDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ToolAndConsumableActions_EmitDistinctSoundCuesPerToolAndItemType()
    {
        var loader = new JsonLevelLoader();
        DifficultyCatalog catalog = loader.LoadDifficultyCatalog();
        var items = ItemFactory.CreateDefault();
        items.RegisterFoodDefinitions(loader.LoadFoods());
        items.RegisterArmorDefinitions(loader.LoadArmorItems());

        var level = new IslandLevel("ISLAND_01", "Audio Cue Test", "Forest", "KingSlime", 32, 32, 16, 16);
        level.SetTileType(17, 16, TileType.Grass);

        var player = new Player("player_audio", level.PlayerSpawnWorldPosition, inventory: Inventory.CreateUnlimited());
        player.EnableExplicitHotbar();
        player.Inventory.TryAddItem(items.ResolveItem("copper_sword"), 1);
        player.Inventory.TryAddItem(items.ResolveItem("wooden_bow"), 1);
        player.Inventory.TryAddItem(items.ResolveItem("wood_axe"), 1);
        player.Inventory.TryAddItem(items.ResolveItem("wood_pickaxe"), 1);
        player.Inventory.TryAddItem(items.ResolveItem("wood_hoe"), 1);
        player.Inventory.TryAddItem(items.ResolveItem("cooked_meat"), 2);
        player.Inventory.TryAddItem(items.ResolveItem("health_potion"), 2);
        player.Inventory.TryAddItem(items.ResolveItem("copper_helmet"), 1);

        Assert.True(player.TryReplaceHotbarSlot("copper_sword", 0, out _));
        Assert.True(player.TryReplaceHotbarSlot("wooden_bow", 1, out _));
        Assert.True(player.TryReplaceHotbarSlot("wood_axe", 3, out _));
        Assert.True(player.TryReplaceHotbarSlot("wood_pickaxe", 4, out _));
        Assert.True(player.TryReplaceHotbarSlot("wood_hoe", 5, out _));
        Assert.True(player.TryReplaceHotbarSlot("cooked_meat", 6, out _));
        Assert.True(player.TryReplaceHotbarSlot("health_potion", 7, out _));

        var enemyFactory = EnemyFactory.CreateDefault(difficultyCatalog: catalog);
        using var session = new GameSession(level, player, items.ResolveItem, difficulty: Difficulty.Normal, difficultyCatalog: catalog);
        var cues = new List<string>();
        session.OnSoundEffectRequested += cues.Add;
        session.Start();

        // 1. Axe -> Tree
        ResourceNode tree = enemyFactory.CreateResourceNode(ResourceType.Tree, player.Center + new Vector2D(32f, 0f));
        session.AddResource(tree);
        player.SelectHotbarSlot(3);
        session.Update(0.6f);
        Assert.True(session.ExecutePrimaryAction(tree.Center));
        Assert.Contains("tool_axe", cues);
        cues.Clear();

        // 2. Pickaxe -> StoneRock
        ResourceNode rock = enemyFactory.CreateResourceNode(ResourceType.StoneRock, player.Center + new Vector2D(-32f, 0f));
        session.AddResource(rock);
        player.SelectHotbarSlot(4);
        session.Update(0.6f);
        Assert.True(session.ExecutePrimaryAction(rock.Center));
        Assert.Contains("tool_pickaxe", cues);
        cues.Clear();

        // 3. Hoe -> Till Grass
        player.SelectHotbarSlot(5);
        session.Update(0.6f);
        Assert.True(session.ExecutePrimaryAction(level.TileToWorldCenter(17, 16)));
        Assert.Contains("tool_hoe", cues);
        cues.Clear();

        // 4. Sword -> Melee enemy
        Enemy slime = enemyFactory.CreateEnemy("Slime", player.Center + new Vector2D(0f, 32f));
        session.AddEnemy(slime);
        player.SelectHotbarSlot(0);
        session.Update(0.6f);
        Assert.True(session.ExecutePrimaryAction(slime.Center));
        Assert.Contains("tool_sword", cues);
        Assert.Contains("hit", cues);
        cues.Clear();

        // 5. Bow -> Ranged arrow shot
        player.SelectHotbarSlot(1);
        session.Update(0.6f);
        Assert.True(session.ExecutePrimaryAction(player.Center + new Vector2D(96f, 0f)));
        Assert.Contains("tool_bow", cues);
        cues.Clear();

        // 6. Food -> eat, Potion -> potion, Armor -> armor_equip
        player.TakeDirectDamage(25);
        player.RestoreHungerExact(40f, 100f);
        player.SelectHotbarSlot(6);
        Assert.True(session.TryUseSelectedHotbarItem());
        Assert.Contains("eat", cues);
        cues.Clear();

        player.TakeDirectDamage(35);
        player.SelectHotbarSlot(7);
        Assert.True(session.TryUseSelectedHotbarItem());
        Assert.Contains("potion", cues);
        cues.Clear();

        Assert.True(session.TryEquipArmorFromInventory("copper_helmet"));
        Assert.Contains("armor_equip", cues);
    }
}
