using System;
using System.Collections.Generic;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Farming;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Decorator;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Tests.Mocks;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class SurvivalAndFarmingTests
{
    [Fact]
    public void Hunger_DrainsOverTimeAndTriggersPeriodicStarvationDamage()
    {
        var player = new Player(
            "player_1",
            new Vector2D(64f, 64f),
            maxHp: 100,
            maxHunger: 10f,
            hungerDrainPerSecond: 5f,
            starvationInterval: 2f,
            starvationDamage: 4);

        player.Update(1.0f);
        Assert.Equal(5f, player.CurrentHunger, precision: 2);
        Assert.False(player.IsStarving);
        Assert.Equal(100, player.CurrentHp);

        player.Update(1.5f);
        Assert.Equal(0f, player.CurrentHunger);
        Assert.True(player.IsStarving);

        player.Update(2.1f);
        Assert.Equal(96, player.CurrentHp);

        player.RestoreHunger(25f);
        Assert.Equal(10f, player.CurrentHunger);
        Assert.False(player.IsStarving);
    }

    [Fact]
    public void FoodsJsonAndBuffs_ApplyStrategyEffectsFoodDamageDecoratorAndExpire()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<FoodDefinition> foods = loader.LoadFoods();
        Assert.True(foods.Count >= 9);

        var itemFactory = ItemFactory.CreateDefault();
        itemFactory.RegisterFoodDefinitions(foods);

        var player = new Player("player_1", new Vector2D(64f, 64f), maxHp: 100, moveSpeed: 100f);
        player.TakeDamage(40);
        player.Update(20f);
        float hungerBefore = player.CurrentHunger;

        Item carrotStew = itemFactory.ResolveItem("carrot_stew");
        Item pumpkinPie = itemFactory.ResolveItem("pumpkin_pie");
        Item sword = itemFactory.ResolveItem("copper_sword");

        player.Inventory.TryAddItem(sword, 1);
        player.Inventory.TryAddItem(carrotStew, 1);
        player.Inventory.TryAddItem(pumpkinPie, 1);
        player.SelectHotbarSlot(0);

        Assert.True(carrotStew.TryUse(player));
        Assert.True(player.CurrentHunger > hungerBefore);
        Assert.Equal(122f, player.EffectiveMoveSpeed, precision: 2);

        // Regen ticks 2 HP/s over 2 seconds = +4 HP healed on top of stew's +25 instant heal.
        int hpAfterStew = player.CurrentHp;
        player.Update(2.0f);
        Assert.True(player.CurrentHp >= hpAfterStew + 3);

        // Eat pumpkin pie -> Warrior Feast (+12 damage) wraps EquippedWeapon via FoodDamageBuffDecorator.
        Assert.True(pumpkinPie.TryUse(player));
        Assert.NotNull(player.EffectiveEquippedWeapon);
        Assert.IsType<FoodDamageBuffDecorator>(player.EffectiveEquippedWeapon);
        Assert.Equal(player.EquippedWeapon!.ComputeDamage() + 12, player.EffectiveEquippedWeapon!.ComputeDamage());

        // Advance past both buffs (45 seconds) -> buffs expire cleanly; speed still reflects hunger ratio cleanly.
        player.Update(45f);
        Assert.Equal(0, player.Buffs.ActiveBuffCount);
        Assert.Equal(100f * player.HungerSpeedMultiplier, player.EffectiveMoveSpeed, precision: 2);
        Assert.Same(player.EquippedWeapon, player.EffectiveEquippedWeapon);
    }

    [Fact]
    public void FarmingSystem_TillPlantGrowHarvestAndRoundTripSave_WorksEndToEnd()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<CropDefinition> crops = loader.LoadCrops();
        Assert.True(crops.Count >= 3);

        var registry = new CropRegistry();
        registry.RegisterRange(crops);
        var itemFactory = ItemFactory.CreateDefault();
        itemFactory.RegisterCropDefinitions(crops);
        var enemyFactory = EnemyFactory.CreateDefault();

        var level = new IslandLevel("ISLAND_FARM", "Farm Isle", "Forest", "KingSlime", 20, 20, 10, 10);
        var player = new Player("player_1", level.TileToWorldCenter(10, 10));
        player.Inventory.TryAddItem(itemFactory.ResolveItem("wood_hoe"), 1);
        player.Inventory.TryAddItem(itemFactory.ResolveItem("wheat_seed"), 3);
        player.Inventory.TryAddItem(itemFactory.ResolveItem("wooden_wall"), 1);

        using var session = new GameSession(level, player, itemFactory.ResolveItem, cropRegistry: registry, gameplaySeed: 42);
        session.Start();

        // Blocked water tile cannot be tilled; structure tile cannot be tilled; walkable grass tile can.
        Assert.False(session.Farming.TryTillTile(level, 0, 0, session.FindStructureAtTile));
        session.AddStructure(enemyFactory.CreateStructure(StructureType.WoodenWall, 11, 10, level.TileSize));
        Assert.False(session.Farming.TryTillTile(level, 11, 10, session.FindStructureAtTile));

        // Use Hoe via primary action on grass tile (12, 10).
        player.SelectHotbarSlot(0);
        Vector2D plotWorld = level.TileToWorldCenter(12, 10);
        Assert.True(session.ExecutePrimaryAction(plotWorld));
        FarmPlot? plot = session.Farming.GetPlotAt(12, 10);
        Assert.NotNull(plot);
        Assert.False(plot!.IsPlanted);

        // Plant Wheat Seed via secondary action.
        player.SelectHotbarSlot(1);
        Assert.True(session.ExecuteSecondaryAction(plotWorld, enemyFactory));
        Assert.True(plot.IsPlanted);
        Assert.Equal("wheat_crop", plot.CropId);
        Assert.Equal(2, player.Inventory.GetItemCount("wheat_seed"));

        // Grow partway and round-trip save/load.
        session.Update(14.0f);
        float expectedGrowth = plot.GrowthElapsedSeconds;
        float expectedHunger = player.CurrentHunger;

        var memorySave = new InMemorySaveStorage();
        memorySave.Save(1, session.CreateSnapshot());
        GameStateDTO loaded = memorySave.Load(1);

        var restoredLevel = new IslandLevel("ISLAND_FARM", "Farm Isle", "Forest", "KingSlime", 20, 20, 10, 10);
        var restoredPlayer = new Player("player_1", restoredLevel.PlayerSpawnWorldPosition);
        using var restoredSession = new GameSession(restoredLevel, restoredPlayer, itemFactory.ResolveItem, cropRegistry: registry, gameplaySeed: 42);
        restoredSession.RestoreFromSnapshot(loaded, enemyFactory);
        restoredSession.Start();

        Assert.Equal(expectedHunger, restoredPlayer.CurrentHunger, precision: 2);
        FarmPlot? restoredPlot = restoredSession.Farming.GetPlotAt(12, 10);
        Assert.NotNull(restoredPlot);
        Assert.True(restoredPlot!.IsPlanted);
        Assert.Equal(expectedGrowth, restoredPlot.GrowthElapsedSeconds, precision: 2);

        // Finish growth to mature stage and harvest via primary action.
        restoredSession.Update(12.0f);
        Assert.True(restoredPlot.IsMature);
        Assert.True(restoredSession.ExecutePrimaryAction(restoredLevel.TileToWorldCenter(12, 10)));
        Assert.False(restoredPlot.IsPlanted);
        Assert.Contains(restoredSession.DroppedItems, d => d.IsActive && d.ItemId == "wheat");
    }
}
