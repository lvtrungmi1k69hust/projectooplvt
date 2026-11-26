using System;
using System.Collections.Generic;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Loot;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Tests.Mocks;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class EquipmentLootAndChestTests
{
    [Fact]
    public void ArmorSlots_EquipSwapDefenseMitigationAndDirectStarvationDamage_WorkCorrectly()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<ArmorDefinition> armorDefs = loader.LoadArmorItems();
        Assert.True(armorDefs.Count >= 9);

        var itemFactory = ItemFactory.CreateDefault();
        itemFactory.RegisterArmorDefinitions(armorDefs);

        var player = new Player("player_1", new Vector2D(64f, 64f), maxHp: 100);
        Item leatherHelm = itemFactory.ResolveItem("leather_helmet");
        Item ironHelm = itemFactory.ResolveItem("iron_helmet");
        Item ironChest = itemFactory.ResolveItem("iron_chestplate");
        Item ironLegs = itemFactory.ResolveItem("iron_greaves");

        Assert.True(leatherHelm.TryUse(player));
        Assert.Equal(2, player.TotalDefense);

        // Swapping Head armor returns displaced leather helmet to inventory.
        Assert.True(ironHelm.TryUse(player));
        Assert.Equal("iron_helmet", player.Equipment.Head?.Id);
        Assert.Equal(1, player.Inventory.GetItemCount("leather_helmet"));

        Assert.True(ironChest.TryUse(player));
        Assert.True(ironLegs.TryUse(player));
        Assert.Equal(21, player.TotalDefense); // 6 + 9 + 6

        // Incoming 30 raw dmg - 21 defense = 9 damage.
        player.TakeDamage(30);
        Assert.Equal(91, player.CurrentHp);

        // Incoming 5 raw dmg (< defense) enforces minimum 1 damage.
        player.TakeDamage(5);
        Assert.Equal(90, player.CurrentHp);

        // Starvation direct damage bypasses armor mitigation.
        player.TakeDirectDamage(3);
        Assert.Equal(87, player.CurrentHp);
    }

    [Fact]
    public void LootTablesJson_RollsGuaranteedAndProbabilisticDropsWithinBounds()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<LootTable> tables = loader.LoadLootTables();
        Assert.True(tables.Count >= 13);

        var registry = new LootTableRegistry();
        registry.RegisterRange(tables);
        registry.Register(new LootTable("NeverDropEnemy", new[]
        {
            new LootEntry("wood", 0.0f, 1, 2, guaranteed: false),
            new LootEntry("gold_ore", 1.0f, 2, 4, guaranteed: true)
        }));

        var buffer = new List<LootDropResult>();
        var rng = new Random(12345);

        Assert.True(registry.RollDrops("NeverDropEnemy", rng, buffer));
        Assert.Single(buffer);
        Assert.Equal("gold_ore", buffer[0].ItemId);
        Assert.InRange(buffer[0].Quantity, 2, 4);

        Assert.True(registry.RollDrops("KingSlime", rng, buffer));
        Assert.Contains(buffer, d => d.ItemId == "slime_gel" && d.Quantity >= 4 && d.Quantity <= 6);
        Assert.Contains(buffer, d => d.ItemId == "copper_ore" && d.Quantity >= 3 && d.Quantity <= 5);
        Assert.Contains(buffer, d => d.ItemId == "health_potion" && d.Quantity == 2);
    }

    [Fact]
    public void StorageChest_DepositWithdrawDestroySpillAndSaveRoundTrip_PreserveAllItemsAndArmor()
    {
        var level = new IslandLevel("ISLAND_CHEST", "Chest Isle", "Forest", "KingSlime", 20, 20, 10, 10);
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);
        var itemFactory = ItemFactory.CreateDefault();
        var enemyFactory = EnemyFactory.CreateDefault();

        player.Inventory.TryAddItem(itemFactory.ResolveItem("copper_ore"), 10);
        player.Inventory.TryAddItem(itemFactory.ResolveItem("wooden_chest"), 2);
        player.Inventory.TryAddItem(itemFactory.ResolveItem("copper_helmet"), 1);
        player.SelectHotbarSlot(2);
        Assert.True(new ArmorItem("copper_helmet", "Copper Helmet", ArmorSlot.Head, 4).TryUse(player));
        Assert.True(itemFactory.ResolveItem("iron_chestplate").TryUse(player));
        Assert.True(itemFactory.ResolveItem("leather_greaves").TryUse(player));

        using var session = new GameSession(level, player, itemFactory.ResolveItem);
        session.Start();

        // Place chest near player at (12, 10) without overlapping player bounds.
        player.SelectHotbarSlot(1);
        Vector2D chestWorld = level.TileToWorldCenter(12, 10);
        Assert.True(session.ExecuteSecondaryAction(chestWorld, enemyFactory));

        Structure? chest = session.FindNearestChest(player.Center);
        Assert.NotNull(chest);
        Assert.True(chest!.HasInventory);

        Assert.True(session.TryDepositToChest(chest, "copper_ore", 6));
        Assert.Equal(4, player.Inventory.GetItemCount("copper_ore"));
        Assert.Equal(6, chest.StorageInventory!.GetItemCount("copper_ore"));

        Assert.True(session.TryWithdrawFromChest(chest, "copper_ore", 2));
        Assert.Equal(6, player.Inventory.GetItemCount("copper_ore"));
        Assert.Equal(4, chest.StorageInventory.GetItemCount("copper_ore"));

        // Save/Load round-trip preserves equipped armor and chest items.
        var storage = new InMemorySaveStorage();
        storage.Save(1, session.CreateSnapshot());
        GameStateDTO loaded = storage.Load(1);

        var restoredLevel = new IslandLevel("ISLAND_CHEST", "Chest Isle", "Forest", "KingSlime", 20, 20, 10, 10);
        var restoredPlayer = new Player("player_1", restoredLevel.PlayerSpawnWorldPosition);
        using var restoredSession = new GameSession(restoredLevel, restoredPlayer, itemFactory.ResolveItem);
        restoredSession.RestoreFromSnapshot(loaded, enemyFactory);
        restoredSession.Start();

        Assert.Equal("copper_helmet", restoredSession.Equipment.Head?.Id);
        Assert.Equal("iron_chestplate", restoredSession.Equipment.Body?.Id);
        Assert.Equal("leather_greaves", restoredSession.Equipment.Legs?.Id);
        Assert.Equal(15, restoredSession.Player.TotalDefense);

        Structure? restoredChest = restoredSession.FindNearestChest(restoredPlayer.Center);
        Assert.NotNull(restoredChest);
        Assert.Equal(4, restoredChest!.StorageInventory!.GetItemCount("copper_ore"));

        // Destroying chest spills remaining stored items as active DroppedItem.
        restoredChest.TakeDamage(restoredChest.MaxHp);
        restoredSession.Update(1f / 60f);
        Assert.Contains(restoredSession.DroppedItems, d => d.IsActive && d.ItemId == "copper_ore" && d.Quantity == 4);
    }
}
