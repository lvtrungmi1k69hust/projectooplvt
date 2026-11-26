using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Quests;
using MiniNecesse.Core.Rules;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class WeatherAndQuestTests
{
    [Fact]
    public void WeatherSystem_TransitionsDeterministicallyWithDayNightCycleAndPersistsInSave()
    {
        var cycleA = new DayNightCycle(dayDurationSeconds: 10f, nightDurationSeconds: 10f);
        var cycleB = new DayNightCycle(dayDurationSeconds: 10f, nightDurationSeconds: 10f);
        using var weatherA = new WeatherSystem(cycleA, seed: 555);
        using var weatherB = new WeatherSystem(cycleB, seed: 555);

        var observedTypes = new HashSet<WeatherType> { weatherA.CurrentWeather };
        for (int i = 0; i < 16; i++)
        {
            cycleA.Advance(10f);
            cycleB.Advance(10f);
            Assert.Equal(weatherA.CurrentWeather, weatherB.CurrentWeather);
            observedTypes.Add(weatherA.CurrentWeather);
        }

        Assert.True(observedTypes.Count >= 2);

        // Save/Load weather persistence inside GameSession
        var level = new IslandLevel("ISLAND_01", "Weather Isle", "Forest", "KingSlime", 24, 24, 12, 12);
        var items = ItemFactory.CreateDefault();
        using var session = new GameSession(level, new Player("p1", level.PlayerSpawnWorldPosition), items.ResolveItem);
        session.Weather.RestoreWeather(WeatherType.Rain, 0.72f);

        var storage = new LocalSaveStorage();
        storage.Save(1, session.CreateSnapshot());
        GameStateDTO dto = storage.Load(1);
        Assert.Equal("Rain", dto.Weather);

        var level2 = new IslandLevel("ISLAND_01", "Weather Isle", "Forest", "KingSlime", 24, 24, 12, 12);
        using var restored = new GameSession(level2, new Player("p1", level2.PlayerSpawnWorldPosition), items.ResolveItem);
        restored.RestoreFromSnapshot(dto, EnemyFactory.CreateDefault());
        Assert.Equal(WeatherType.Rain, restored.Weather.CurrentWeather);
    }

    [Fact]
    public void QuestSystem_JsonQuestsAdvanceRewardScoreItemsAndPersistAcrossSaveLoad()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<QuestDefinition> defs = loader.LoadQuests();
        Assert.Equal(4, defs.Count);

        var level = new IslandLevel("ISLAND_01", "Quest Isle", "Forest", "KingSlime", 32, 32, 16, 16);
        var items = ItemFactory.CreateDefault();
        IReadOnlyList<FoodDefinition> foods = loader.LoadFoods();
        if (foods.Count > 0)
        {
            items.RegisterFoodDefinitions(foods);
        }

        var player = new Player("p1", level.PlayerSpawnWorldPosition);
        player.Inventory.TryAddItem(items.ResolveItem("wood_axe"), 1);
        player.SelectHotbarSlot(0);

        using var session = new GameSession(level, player, items.ResolveItem, questDefinitions: defs);
        session.Start();
        var factory = EnemyFactory.CreateDefault();

        // Chop 3 trees to complete "gather_wood" quest (+100 bonus score + 1 bread reward)
        for (int i = 0; i < 3; i++)
        {
            ResourceNode tree = factory.CreateResourceNode(ResourceType.Tree, player.Center + new Vector2D(28f, 0f));
            session.AddResource(tree);
            while (tree.IsAlive)
            {
                session.Update(0.5f);
                session.ExecutePrimaryAction(tree.Center);
            }
        }

        Assert.True(session.Quests.Quests[0].IsCompleted);
        Assert.Equal(1, player.Inventory.GetItemCount("bread"));
        Assert.Equal((3 * RuleEngine.HARVEST_SCORE) + 100, session.RuleEngine.Score);
        Assert.Equal("build_shelter", session.Quests.ActiveQuest!.Definition.QuestId);

        // Save and reload verifies quest progress persistence
        var storage = new LocalSaveStorage();
        storage.Save(1, session.CreateSnapshot());
        GameStateDTO dto = storage.Load(1);

        var level2 = new IslandLevel("ISLAND_01", "Quest Isle", "Forest", "KingSlime", 32, 32, 16, 16);
        using var restored = new GameSession(level2, new Player("p1", level2.PlayerSpawnWorldPosition), items.ResolveItem, questDefinitions: defs);
        restored.RestoreFromSnapshot(dto, factory);

        Assert.True(restored.Quests.Quests[0].IsCompleted);
        Assert.False(restored.Quests.Quests[1].IsCompleted);
        Assert.Equal("build_shelter", restored.Quests.ActiveQuest!.Definition.QuestId);
    }

    private sealed class LocalSaveStorage : ISaveStorage
    {
        private readonly Dictionary<int, GameStateDTO> _slots = new();
        public void Save(int slotIndex, GameStateDTO snapshot) => _slots[slotIndex] = snapshot;
        public GameStateDTO Load(int slotIndex) => _slots[slotIndex];
        public bool Exists(int slotIndex) => _slots.ContainsKey(slotIndex);
        public void Delete(int slotIndex) => _slots.Remove(slotIndex);
    }
}
