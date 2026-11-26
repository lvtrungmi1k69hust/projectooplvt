using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.Tutorial;
using MiniNecesse.Data;

namespace MiniNecesse.Core.Tests;

public sealed class DifficultyAndHighScoreTests
{
    [Fact]
    public void Difficulties_JsonAndDecorator_EnforceZeroDamageInTutorialAndScaleNormalVsHard()
    {
        var loader = new JsonLevelLoader();
        DifficultyCatalog catalog = loader.LoadDifficultyCatalog();
        DifficultyProfile tutorial = catalog.GetProfile(Difficulty.Tutorial);
        DifficultyProfile normal = catalog.GetProfile(Difficulty.Normal);
        DifficultyProfile hard = catalog.GetProfile(Difficulty.Hard);

        Assert.Equal(0f, tutorial.DamageTakenMultiplier);
        Assert.True(tutorial.PreventPlayerDeath);
        Assert.True(tutorial.EnableTutorialGuide);
        Assert.Equal(1f, normal.DamageTakenMultiplier);
        Assert.True(hard.DamageTakenMultiplier > normal.DamageTakenMultiplier);
        Assert.True(hard.EnemyDamageMultiplier > normal.EnemyDamageMultiplier);
        Assert.True(hard.RaidIntensity > normal.RaidIntensity);

        // 1. Tutorial Session: enemy strikes & starvation deal 0 damage -> player stays 100/100 HP & Playing
        var levelTut = new IslandLevel("ISLAND_01", "Tutorial Isle", "Forest", "KingSlime", 24, 24, 12, 12);
        var items = ItemFactory.CreateDefault();
        var tutPlayer = new Player(
            "player_tut",
            levelTut.PlayerSpawnWorldPosition,
            maxHp: 100,
            hungerDrainPerSecond: 100f,
            starvationInterval: 0.5f,
            starvationDamage: 10);
        using var tutSession = new GameSession(
            levelTut,
            tutPlayer,
            items.ResolveItem,
            difficulty: Difficulty.Tutorial,
            difficultyCatalog: catalog,
            difficultyProfile: tutorial,
            tutorialSteps: loader.LoadTutorialSteps());
        tutSession.Start();

        var enemyFactory = EnemyFactory.CreateDefault(difficultyCatalog: catalog);
        Enemy slayer = enemyFactory.CreateEnemy("ZombieRaider", tutPlayer.Position + new Vector2D(8f, 0f), Difficulty.Tutorial);
        tutSession.AddEnemy(slayer);

        for (int i = 0; i < 180; i++)
        {
            tutSession.Update(1f / 60f);
        }

        Assert.Equal(0f, tutPlayer.CurrentHunger);
        Assert.Equal(100, tutPlayer.CurrentHp);
        Assert.True(tutPlayer.IsAlive);
        Assert.Equal(GameState.Playing, tutSession.CurrentState);

        // 2. Normal vs Hard damage scaling via DifficultyDamageMitigationDecorator & EnemyFactory
        var pNormal = new Player("p_norm", Vector2D.Zero, maxHp: 200);
        var pHard = new Player("p_hard", Vector2D.Zero, maxHp: 200);
        pNormal.SetDamageMitigationStrategy(new DifficultyDamageMitigationDecorator(FlatDefenseMitigationStrategy.Instance, normal));
        pHard.SetDamageMitigationStrategy(new DifficultyDamageMitigationDecorator(FlatDefenseMitigationStrategy.Instance, hard));

        Enemy orcNormal = enemyFactory.CreateEnemy("OrcBrute", Vector2D.Zero, Difficulty.Normal);
        Enemy orcHard = enemyFactory.CreateEnemy("OrcBrute", Vector2D.Zero, Difficulty.Hard);
        Assert.True(orcHard.MaxHp > orcNormal.MaxHp);
        Assert.True(orcHard.AttackDamage > orcNormal.AttackDamage);

        pNormal.TakeDamage(orcNormal.AttackDamage);
        pHard.TakeDamage(orcHard.AttackDamage);
        int normTaken = pNormal.MaxHp - pNormal.CurrentHp;
        int hardTaken = pHard.MaxHp - pHard.CurrentHp;
        Assert.True(hardTaken > normTaken);

        // 3. Raid intensity scaling in RaidWaveSpawner
        var waves = new[] { new WaveDefinition(1, new[] { new WaveSpawnEntry("Slime", 4) }) };
        var normSpawner = new RaidWaveSpawner(levelTut, Difficulty.Normal, waves, enemyFactory.CreateEnemy, seed: 1, difficultyProfile: normal);
        var hardSpawner = new RaidWaveSpawner(levelTut, Difficulty.Hard, waves, enemyFactory.CreateEnemy, seed: 1, difficultyProfile: hard);
        var spawnedNorm = new List<Enemy>();
        var spawnedHard = new List<Enemy>();
        normSpawner.SpawnNextWave(pNormal, spawnedNorm);
        hardSpawner.SpawnNextWave(pHard, spawnedHard);
        Assert.Equal(4, spawnedNorm.Count);
        Assert.Equal(6, spawnedHard.Count);
    }

    [Fact]
    public void TutorialSteps_JsonSequenceAdvancesThroughAllFiveStepsAndPersistsInSave()
    {
        var loader = new JsonLevelLoader();
        IReadOnlyList<TutorialStepDefinition> steps = loader.LoadTutorialSteps();
        DifficultyCatalog catalog = loader.LoadDifficultyCatalog();
        Assert.Equal(5, steps.Count);

        var level = new IslandLevel("ISLAND_01", "Tut Guide", "Forest", "KingSlime", 32, 32, 16, 16);
        var items = ItemFactory.CreateDefault();
        var recipes = loader.LoadRecipes();
        var player = new Player("player_1", level.PlayerSpawnWorldPosition);
        player.Inventory.TryAddItem(items.ResolveItem("wood_axe"), 1);
        player.Inventory.TryAddItem(items.ResolveItem("wood"), 8);
        player.Inventory.TryAddItem(items.ResolveItem("copper_ore"), 6);
        player.Inventory.TryAddItem(items.ResolveItem("wooden_wall"), 2);
        player.SelectHotbarSlot(0);

        using var session = new GameSession(
            level,
            player,
            items.ResolveItem,
            difficulty: Difficulty.Tutorial,
            difficultyCatalog: catalog,
            tutorialSteps: steps);
        session.Start();
        var enemyFactory = EnemyFactory.CreateDefault();

        Assert.True(session.Tutorial.IsEnabled);
        Assert.Equal("chop_tree", session.Tutorial.CurrentStep!.StepId);

        // Step 1: Chop Tree
        ResourceNode tree = enemyFactory.CreateResourceNode(ResourceType.Tree, player.Center + new Vector2D(28f, 0f));
        session.AddResource(tree);
        while (tree.IsAlive)
        {
            session.Update(0.5f);
            session.ExecutePrimaryAction(tree.Center);
        }
        Assert.Equal("craft_workbench", session.Tutorial.CurrentStep!.StepId);

        // Step 2: Craft Workbench
        CraftingRecipe workbenchRecipe = Assert.Single(recipes, r => r.OutputItemId == "workbench");
        Assert.True(session.TryCraft(workbenchRecipe));
        Assert.Equal("craft_sword", session.Tutorial.CurrentStep!.StepId);

        // Place workbench nearby, then Step 3: Craft Copper Sword
        session.AddStructure(enemyFactory.CreateStructure(StructureType.Workbench, 14, 16, level.TileSize));
        CraftingRecipe swordRecipe = Assert.Single(recipes, r => r.OutputItemId == "copper_sword");
        Assert.True(session.TryCraft(swordRecipe));
        Assert.Equal("attack_enemy", session.Tutorial.CurrentStep!.StepId);

        // Snapshot mid-tutorial at step index 3 and restore into fresh session
        var storage = new LocalSaveStorage();
        session.Update(1.25f);
        storage.Save(1, session.CreateSnapshot());
        GameStateDTO loaded = storage.Load(1);
        Assert.Equal(3, loaded.TutorialStepIndex);
        Assert.True(loaded.PlayTimeSeconds >= 1.2);

        var level2 = new IslandLevel("ISLAND_01", "Tut Guide", "Forest", "KingSlime", 32, 32, 16, 16);
        var player2 = new Player("player_1", level2.PlayerSpawnWorldPosition);
        using var restored = new GameSession(
            level2,
            player2,
            items.ResolveItem,
            difficulty: Difficulty.Normal,
            difficultyCatalog: catalog,
            tutorialSteps: steps);
        restored.RestoreFromSnapshot(loaded, enemyFactory);
        restored.Start();

        Assert.Equal(Difficulty.Tutorial, restored.Difficulty);
        Assert.True(restored.Tutorial.IsEnabled);
        Assert.Equal("attack_enemy", restored.Tutorial.CurrentStep!.StepId);

        // Step 4: Attack enemy
        Enemy slime = enemyFactory.CreateEnemy("Slime", restored.Player.Center + new Vector2D(28f, 0f));
        restored.AddEnemy(slime);
        restored.Update(0.6f);
        Assert.True(restored.ExecutePrimaryAction(slime.Center));
        Assert.Equal("place_wall", restored.Tutorial.CurrentStep!.StepId);

        // Step 5: Select wooden_wall and place 2 tiles away (no player overlap)
        int wallSlot = FindSlotIndex(restored.Inventory, "wooden_wall");
        Assert.True(wallSlot >= 0);
        restored.Player.SelectHotbarSlot(wallSlot);
        Assert.True(restored.ExecuteSecondaryAction(level2.TileToWorldCenter(18, 16), enemyFactory));
        Assert.True(restored.Tutorial.IsCompleted);
        Assert.Null(restored.Tutorial.CurrentStep);
    }

    [Fact]
    public void HighScoreRepository_IsolatesBucketsReadsLegacyAndSurvivesCorruptJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"mininecesse_hs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string filePath = Path.Combine(tempDir, "highscore.json");
            var repo = new JsonHighScoreRepository(filePath);

            // 1. Legacy JSON without mode or playTimeSeconds deserializes with safe defaults
            const string legacyJson = """
            [
              {
                "playerName": "OldHero",
                "score": 950,
                "islandId": "ISLAND_03",
                "difficulty": "Normal",
                "achievedAtUtc": "2025-05-01T10:00:00Z"
              }
            ]
            """;
            File.WriteAllText(filePath, legacyJson);

            IReadOnlyList<HighScoreEntry> legacyScores = repo.LoadTopScores(GameMode.Campaign, Difficulty.Normal);
            HighScoreEntry legacy = Assert.Single(legacyScores);
            Assert.Equal("OldHero", legacy.PlayerName);
            Assert.Equal("Campaign", legacy.Mode);
            Assert.Equal(0.0, legacy.PlayTimeSeconds);

            // 2. Bucket isolation: Tutorial scores stay separate, Normal trims strictly to Top 10, Endless/Hard preserved
            repo.RecordScore(new HighScoreEntry("TutChamp", 99999, "ISLAND_01", "Tutorial", DateTime.UtcNow, "Campaign", 120));
            repo.RecordScore(new HighScoreEntry("EndlessMaster", 8800, "ENDLESS_77", "Hard", DateTime.UtcNow, "Endless", 450));
            for (int i = 1; i <= 12; i++)
            {
                repo.RecordScore(new HighScoreEntry($"Runner_{i}", 100 * i, "ISLAND_02", "Normal", DateTime.UtcNow.AddSeconds(i), "Campaign", 60 + i));
            }

            IReadOnlyList<HighScoreEntry> tutBucket = repo.LoadTopScores(GameMode.Campaign, Difficulty.Tutorial);
            IReadOnlyList<HighScoreEntry> normBucket = repo.LoadTopScores(GameMode.Campaign, Difficulty.Normal);
            IReadOnlyList<HighScoreEntry> endBucket = repo.LoadTopScores(GameMode.Endless, Difficulty.Hard);

            Assert.Single(tutBucket);
            Assert.Equal("TutChamp", tutBucket[0].PlayerName);

            Assert.Equal(10, normBucket.Count);
            Assert.DoesNotContain(normBucket, s => s.PlayerName == "TutChamp");
            Assert.DoesNotContain(normBucket, s => s.PlayerName == "EndlessMaster");
            Assert.Equal(1200, normBucket[0].Score); // Runner_12 top score, 13 entries trimmed cleanly to Top 10

            Assert.Single(endBucket);
            Assert.Equal("EndlessMaster", endBucket[0].PlayerName);

            // 3. Corrupted JSON file does NOT throw/crash and recovers on subsequent write
            File.WriteAllText(filePath, "{ INVALID JSON !!! [[[ truncated");
            IReadOnlyList<HighScoreEntry> corruptRead = repo.LoadTopScores(GameMode.Campaign, Difficulty.Normal);
            Assert.Empty(corruptRead);

            repo.RecordScore(new HighScoreEntry("Phoenix", 500, "ISLAND_01", "Hard", DateTime.UtcNow, "Campaign", 90));
            HighScoreEntry recovered = Assert.Single(repo.LoadTopScores(GameMode.Campaign, Difficulty.Hard));
            Assert.Equal("Phoenix", recovered.PlayerName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static int FindSlotIndex(Inventory inv, string itemId)
    {
        for (int i = 0; i < inv.Capacity; i++)
        {
            ItemStack? stack = inv.GetStackAt(i);
            if (stack is not null && string.Equals(stack.Item.Id, itemId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
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
