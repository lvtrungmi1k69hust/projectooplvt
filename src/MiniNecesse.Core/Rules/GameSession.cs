using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Farming;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Loot;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Pool;
using MiniNecesse.Core.Patterns.State;
using MiniNecesse.Core.Physics;
using MiniNecesse.Core.Settlement;
using MiniNecesse.Core.Tutorial;
using MiniNecesse.Core.World;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Coordinates an active island survival run using GoF State (<see cref="GameStateMachine"/>),
/// Object Pool (<see cref="ObjectPool{T}"/>), Spatial Hash Grid (<see cref="SpatialHashGrid{T}"/>),
/// and Observer events without any dependency on UI or Audio layers.
/// </summary>
public sealed class GameSession : IDisposable
{
    private const float DEFAULT_WORKBENCH_RADIUS = 80f;
    private const float DEFAULT_PICKUP_RADIUS = 28f;
    private const float DEFAULT_PROJECTILE_LIFETIME = 2.5f;
    private const float NIGHT_REINFORCEMENT_INTERVAL = 18f;
    private const int MAX_ACTIVE_NIGHT_ENEMIES = 18;

    private readonly IslandLevel _level;
    private readonly Player _player;
    private readonly CraftingSystem _craftingSystem;
    private readonly DayNightCycle _dayNightCycle;
    private readonly RuleEngine _ruleEngine;
    private readonly Func<string, Item> _itemResolver;
    private GameMode _gameMode;
    private int _worldSeed;
    private Difficulty _difficulty;
    private DifficultyCatalog _difficultyCatalog;
    private DifficultyProfile _difficultyProfile;
    private readonly TutorialTracker _tutorialTracker;
    private double _elapsedPlayTimeSeconds;

    private readonly GameStateMachine _stateMachine = new();
    private readonly CollisionDetector _collisionDetector = new();
    private readonly PathfindingCoordinator _pathfindingCoordinator = new();
    private readonly SpatialHashGrid<Character> _playerTargetGrid = new(96f);
    private readonly SpatialHashGrid<Player> _playerSpatialGrid = new(96f);
    private readonly List<Character> _characterQueryBuffer = new(32);
    private readonly List<Player> _playerQueryBuffer = new(4);
    private readonly List<Enemy> _waveSpawnBuffer = new(24);

    private readonly ObjectPool<Projectile> _projectilePool;
    private readonly ObjectPool<DroppedItem> _droppedItemPool;

    private readonly List<Enemy> _enemies = new();
    private readonly IReadOnlyList<Enemy> _enemiesView;
    private readonly List<Animal> _animals = new();
    private readonly IReadOnlyList<Animal> _animalsView;
    private readonly List<ResourceNode> _resources = new();
    private readonly IReadOnlyList<ResourceNode> _resourcesView;
    private readonly List<Structure> _structures = new();
    private readonly IReadOnlyList<Structure> _structuresView;
    private readonly Dictionary<GridPoint, Structure> _structureByTile = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly IReadOnlyList<Projectile> _projectilesView;
    private readonly List<DroppedItem> _droppedItems = new();
    private readonly IReadOnlyList<DroppedItem> _droppedItemsView;
    private readonly List<IHudObserver> _hudObservers = new();

    private readonly Action<ResourceNode> _onActivateChunkResource;
    private readonly Action<Structure> _onActivateChunkStructure;
    private readonly Action<ChunkCoord> _onDeactivateChunkEntities;
    private readonly Action<Animal> _onActivateChunkAnimal;
    private readonly Action<Settler> _onActivateChunkSettler;
    private readonly Func<GridPoint, Structure?> _structureLookup;
    private readonly Action<Vector2D, Vector2D, int, string> _settlerProjectileSpawner;

    private readonly ChunkManager _chunkManager;
    private readonly FarmingSystem _farmingSystem;
    private readonly LootTableRegistry _lootTableRegistry;
    private readonly SettlementManager _settlementManager;
    private readonly List<LootDropResult> _lootRollBuffer = new(8);
    private readonly Random _gameplayRng;
    private IReadOnlyList<VillageSite> _villages = Array.Empty<VillageSite>();
    private RaidWaveSpawner? _waveSpawner;
    private Boss? _activeBoss;
    private int _currentWaveIndex;
    private int _pooledProjectileSequence;
    private int _pooledDropSequence;
    private int _lastNotifiedDay = -1;
    private DayPhase _lastNotifiedPhase;
    private int _lastNotifiedProgressBucket = -1;
    private bool _bossDefeated;
    private bool _chunksInitialized;
    private bool _disposed;
    private float _swingEffectRemaining;
    private float _nightReinforcementTimer;
    private Vector2D _lastAimDirection = Vector2D.Right;

    /// <summary>Gets remaining visual swing animation time in seconds (read-only render hint).</summary>
    public float SwingEffectRemaining => _swingEffectRemaining;

    /// <summary>Gets the normalized direction vector of the player's most recent aim/action.</summary>
    public Vector2D LastAimDirection => _lastAimDirection;

    /// <summary>Gets the active chunk streaming manager.</summary>
    public ChunkManager ChunkManager => _chunkManager;

    /// <summary>Gets the farmland tilling, crop growth, and harvest coordinator.</summary>
    public FarmingSystem Farming => _farmingSystem;

    /// <summary>Gets the active data-driven loot table registry.</summary>
    public LootTableRegistry LootTables => _lootTableRegistry;

    /// <summary>Gets the settlement housing, NPC settler AI, and trading coordinator.</summary>
    public SettlementManager Settlement => _settlementManager;

    /// <summary>Gets the player's equipped Head/Body/Legs armor loadout.</summary>
    public EquipmentLoadout Equipment => _player.Equipment;

    /// <summary>Gets the current chunk view distance radius around the player.</summary>
    public int ViewDistanceChunks => _chunkManager.ViewDistanceChunks;

    /// <summary>Gets the active island tilemap.</summary>
    public IslandLevel Level => _level;

    /// <summary>Gets the player character.</summary>
    public Player Player => _player;

    /// <summary>Gets the player's inventory.</summary>
    public Inventory Inventory => _player.Inventory;

    /// <summary>Gets the crafting service.</summary>
    public CraftingSystem CraftingSystem => _craftingSystem;

    /// <summary>Gets the active day/night cycle tracker.</summary>
    public DayNightCycle DayNightCycle => _dayNightCycle;

    /// <summary>Gets the gameplay rule engine.</summary>
    public RuleEngine RuleEngine => _ruleEngine;

    /// <summary>Gets the GoF State Machine controlling session lifecycle.</summary>
    public GameStateMachine StateMachine => _stateMachine;

    /// <summary>Gets the collision detector used for sliding and swept checks.</summary>
    public CollisionDetector CollisionDetector => _collisionDetector;

    /// <summary>Gets the shared AI pathfinding coordinator and path cache.</summary>
    public PathfindingCoordinator PathfindingCoordinator => _pathfindingCoordinator;

    /// <summary>Gets the pooled projectile cache for zero-allocation combat.</summary>
    public ObjectPool<Projectile> ProjectilePool => _projectilePool;

    /// <summary>Gets the pooled dropped-item cache for zero-allocation harvesting.</summary>
    public ObjectPool<DroppedItem> DroppedItemPool => _droppedItemPool;

    /// <summary>Gets the world generation mode (Campaign or Endless).</summary>
    public GameMode GameMode => _gameMode;

    /// <summary>Gets the world generation seed associated with this session.</summary>
    public int WorldSeed => _worldSeed;

    /// <summary>Gets the configured difficulty preset.</summary>
    public Difficulty Difficulty => _difficulty;

    /// <summary>Gets the active JSON difficulty rule profile.</summary>
    public DifficultyProfile DifficultyProfile => _difficultyProfile;

    /// <summary>Gets the step-by-step tutorial tracker active in Tutorial mode.</summary>
    public TutorialTracker Tutorial => _tutorialTracker;

    /// <summary>Gets total accumulated active play time in seconds for high-score telemetry.</summary>
    public double ElapsedPlayTimeSeconds => _elapsedPlayTimeSeconds;

    /// <summary>Gets the current high-level game state from the state machine.</summary>
    public GameState CurrentState => _stateMachine.CurrentStateType;

    /// <summary>Gets the active island boss, if spawned.</summary>
    public Boss? ActiveBoss => _activeBoss;

    /// <summary>Gets whether the island boss has been defeated.</summary>
    public bool IsBossDefeated => _bossDefeated;

    /// <summary>Gets the current raid wave index.</summary>
    public int CurrentWaveIndex => _currentWaveIndex;

    /// <summary>Gets a cached read-only view of all active enemies (including boss).</summary>
    public IReadOnlyList<Enemy> Enemies => _enemiesView;

    /// <summary>Gets a cached read-only view of all active passive animals.</summary>
    public IReadOnlyList<Animal> Animals => _animalsView;

    /// <summary>Gets the procedural villages discovered or generated on this island.</summary>
    public IReadOnlyList<VillageSite> Villages => _villages;

    /// <summary>Gets a cached read-only view of all resource nodes.</summary>
    public IReadOnlyList<ResourceNode> Resources => _resourcesView;

    /// <summary>Gets a cached read-only view of all placed structures.</summary>
    public IReadOnlyList<Structure> Structures => _structuresView;

    /// <summary>Gets a cached read-only view of all active projectiles.</summary>
    public IReadOnlyList<Projectile> Projectiles => _projectilesView;

    /// <summary>Gets a cached read-only view of all loose dropped items.</summary>
    public IReadOnlyList<DroppedItem> DroppedItems => _droppedItemsView;

    /// <summary>Observer event raised when player health changes (currentHp, maxHp).</summary>
    public event Action<int, int>? OnHealthChanged;

    /// <summary>Observer event raised when player inventory contents change.</summary>
    public event Action? OnInventoryChanged;

    /// <summary>Observer event raised when an item is crafted.</summary>
    public event Action<CraftingRecipe, ItemStack>? OnItemCrafted;

    /// <summary>Observer event raised when the player's score changes.</summary>
    public event Action<int>? OnScoreChanged;

    /// <summary>Observer event raised when the session's <see cref="GameState"/> transitions.</summary>
    public event Action<GameState>? OnGameStateChanged;

    /// <summary>Observer event requesting a logical sound cue without coupling Core to any audio API.</summary>
    public event Action<string>? OnSoundEffectRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSession"/> class.
    /// </summary>
    public GameSession(
        IslandLevel level,
        Player player,
        Func<string, Item> itemResolver,
        DayNightCycle? dayNightCycle = null,
        RuleEngine? ruleEngine = null,
        Difficulty difficulty = Difficulty.Normal,
        int viewDistanceChunks = ChunkManager.DEFAULT_VIEW_DISTANCE,
        int minViewDistanceChunks = ChunkManager.DEFAULT_MIN_VIEW_DISTANCE,
        int maxViewDistanceChunks = ChunkManager.DEFAULT_MAX_VIEW_DISTANCE,
        CropRegistry? cropRegistry = null,
        int? gameplaySeed = null,
        LootTableRegistry? lootTableRegistry = null,
        SettlerCatalog? settlerCatalog = null,
        GameMode gameMode = GameMode.Campaign,
        int worldSeed = 0,
        DifficultyCatalog? difficultyCatalog = null,
        DifficultyProfile? difficultyProfile = null,
        IReadOnlyList<TutorialStepDefinition>? tutorialSteps = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(itemResolver);

        _level = level;
        _player = player;
        _itemResolver = itemResolver;
        _craftingSystem = new CraftingSystem(itemResolver);
        _dayNightCycle = dayNightCycle ?? new DayNightCycle();
        _ruleEngine = ruleEngine ?? new RuleEngine();
        _gameMode = gameMode;
        _worldSeed = worldSeed;
        _difficulty = difficulty;
        _difficultyCatalog = difficultyCatalog ?? DifficultyCatalog.CreateDefault();
        _difficultyProfile = difficultyProfile ?? _difficultyCatalog.GetProfile(difficulty);
        _tutorialTracker = new TutorialTracker(_difficultyProfile.EnableTutorialGuide, tutorialSteps);
        _player.SetDamageMitigationStrategy(
            new DifficultyDamageMitigationDecorator(FlatDefenseMitigationStrategy.Instance, _difficultyProfile));
        _chunkManager = new ChunkManager(level, viewDistanceChunks, minViewDistanceChunks, maxViewDistanceChunks);
        _farmingSystem = new FarmingSystem(cropRegistry);
        _lootTableRegistry = lootTableRegistry ?? LootTableRegistry.CreateDefault();
        _gameplayRng = gameplaySeed.HasValue ? new Random(gameplaySeed.Value) : new Random();
        _structureLookup = FindStructureAtTile;
        _settlerProjectileSpawner = (origin, velocity, dmg, ownerId) =>
            SpawnPooledProjectile(origin, velocity, dmg, ownerId, isFromPlayer: true);
        _settlementManager = new SettlementManager(
            itemResolver,
            _pathfindingCoordinator,
            _collisionDetector,
            settlerCatalog,
            _settlerProjectileSpawner);

        _enemiesView = _enemies.AsReadOnly();
        _animalsView = _animals.AsReadOnly();
        _resourcesView = _resources.AsReadOnly();
        _structuresView = _structures.AsReadOnly();
        _projectilesView = _projectiles.AsReadOnly();
        _droppedItemsView = _droppedItems.AsReadOnly();

        _onActivateChunkResource = ActivateChunkResource;
        _onActivateChunkStructure = ActivateChunkStructure;
        _onDeactivateChunkEntities = DeactivateChunkEntities;
        _onActivateChunkAnimal = ActivateChunkAnimal;
        _onActivateChunkSettler = ActivateChunkSettler;

        _projectilePool = new ObjectPool<Projectile>(CreatePooledProjectile, p => p.Deactivate(), initialCapacity: 32, maxCapacity: 256);
        _droppedItemPool = new ObjectPool<DroppedItem>(CreatePooledDrop, d => d.Deactivate(), initialCapacity: 24, maxCapacity: 128);
        _projectilePool.Prewarm(16);
        _droppedItemPool.Prewarm(12);

        ConfigureStateMachine();
        SubscribeInternalEvents();
    }

    /// <summary>Configures procedural resource spawning for newly streamed chunks.</summary>
    public void ConfigureChunkResourceSpawner(
        Action<ChunkCoord, List<ResourceSpawnPoint>>? generator,
        Func<ResourceType, Vector2D, ResourceNode>? resourceFactory)
    {
        _chunkManager.ConfigureResourceSpawner(generator, resourceFactory);
    }

    /// <summary>Configures procedural spawners for chunk resources, village structures, and animals.</summary>
    public void ConfigureChunkWorldSpawners(
        Action<ChunkCoord, List<ResourceSpawnPoint>>? resourceGenerator,
        Func<ResourceType, Vector2D, ResourceNode>? resourceFactory,
        Action<ChunkCoord, List<StructureSpawnPoint>>? structureGenerator = null,
        Func<StructureType, int, int, Structure>? structureFactory = null,
        Action<ChunkCoord, List<AnimalSpawnPoint>>? animalGenerator = null,
        Func<AnimalType, Vector2D, Animal>? animalFactory = null)
    {
        _chunkManager.ConfigureWorldSpawners(
            resourceGenerator,
            resourceFactory,
            structureGenerator,
            structureFactory,
            animalGenerator,
            animalFactory);
    }

    /// <summary>Registers procedural villages for UI telemetry and minimap discovery.</summary>
    public void SetVillages(IReadOnlyList<VillageSite> villages)
    {
        _villages = villages ?? Array.Empty<VillageSite>();
    }

    /// <summary>Returns the village enclosing or immediately adjacent to <paramref name="worldPosition"/>, if any.</summary>
    public VillageSite? FindVillageAt(Vector2D worldPosition)
    {
        for (int i = 0; i < _villages.Count; i++)
        {
            if (_villages[i].IsNearWorldPosition(worldPosition, _level.TileSize))
            {
                return _villages[i];
            }
        }

        return null;
    }

    /// <summary>Updates chunk view distance at runtime and immediately reconciles loaded chunks.</summary>
    public void SetViewDistanceChunks(int viewDistanceChunks)
    {
        _chunkManager.SetViewDistance(viewDistanceChunks);
        if (_chunksInitialized)
        {
            _chunkManager.UpdateAround(
                _player.Center,
                _onActivateChunkResource,
                _onActivateChunkStructure,
                _onDeactivateChunkEntities,
                maxChunksPerTick: _level.TotalChunkCount,
                onActivateAnimal: _onActivateChunkAnimal,
                onActivateSettler: _onActivateChunkSettler);
        }
    }

    /// <summary>Immediately streams the initial view-distance neighborhood around the player.</summary>
    public void InitializeChunksAroundPlayer()
    {
        _chunkManager.InitializeAround(_player.Center, _onActivateChunkResource, _onActivateChunkStructure, _onActivateChunkAnimal, _onActivateChunkSettler);
        _chunksInitialized = true;
    }

    /// <summary>Recruits a friendly settler NPC near the player using configured <c>settlers.json</c> cost.</summary>
    public bool TryRecruitSettler(string professionId, out Settler? recruited, string? customName = null)
    {
        if (_settlementManager.TryRecruit(
                professionId,
                _player,
                _player.Center + new Vector2D(24f, 0f),
                _level,
                _structuresView,
                _structureLookup,
                out recruited,
                customName))
        {
            RequestSound("build");
            return true;
        }

        return false;
    }

    /// <summary>Registers a colony settler (hibernating if its chunk is currently unloaded).</summary>
    public void AddSettler(Settler settler)
    {
        ArgumentNullException.ThrowIfNull(settler);
        if (_level.UsesLazyChunkLoading)
        {
            ChunkCoord coord = _level.GetChunkCoordForWorld(settler.Center);
            if (!_level.IsChunkLoaded(coord))
            {
                _chunkManager.HibernateSettler(coord, settler);
                return;
            }
        }

        ActivateChunkSettler(settler);
    }

    /// <summary>Attaches a <see cref="RaidWaveSpawner"/> triggered automatically on night transitions.</summary>
    public void AttachWaveSpawner(RaidWaveSpawner waveSpawner)
    {
        ArgumentNullException.ThrowIfNull(waveSpawner);
        _waveSpawner = waveSpawner;
    }

    /// <summary>Registers a HUD observer and pushes initial state.</summary>
    public void AttachHudObserver(IHudObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (_hudObservers.Contains(observer))
        {
            return;
        }

        _hudObservers.Add(observer);
        PushFullStateToObserver(observer);
    }

    /// <summary>Unregisters a previously attached HUD observer.</summary>
    public void DetachHudObserver(IHudObserver observer) => _hudObservers.Remove(observer);

    /// <summary>Transitions the state machine into <see cref="GameState.Playing"/>.</summary>
    public void Start()
    {
        if (_level.UsesLazyChunkLoading && !_chunksInitialized)
        {
            InitializeChunksAroundPlayer();
        }

        _stateMachine.TransitionTo(GameState.Playing);
    }

    /// <summary>Transitions the state machine from Playing into <see cref="GameState.Paused"/>.</summary>
    public void Pause()
    {
        if (CurrentState == GameState.Playing)
        {
            _stateMachine.TransitionTo(GameState.Paused);
        }
    }

    /// <summary>Transitions the state machine from Paused back to <see cref="GameState.Playing"/>.</summary>
    public void Resume()
    {
        if (CurrentState == GameState.Paused)
        {
            _stateMachine.TransitionTo(GameState.Playing);
        }
    }

    /// <summary>Transitions the state machine back to <see cref="GameState.MainMenu"/>.</summary>
    public void ReturnToMainMenu() => _stateMachine.TransitionTo(GameState.MainMenu);

    /// <summary>Moves the player with axis-separated tile collision resolution.</summary>
    public void MovePlayer(Vector2D direction, float deltaTime)
    {
        if (CurrentState != GameState.Playing || !_player.IsAlive || direction == Vector2D.Zero)
        {
            return;
        }

        Vector2D normalized = direction.Normalized();
        (int col, int row) = _level.WorldToTile(_player.Center);
        float terrainFactor = _level.TryGetTile(col, row, out Tile? tile) && tile is not null ? tile.MovementSpeedMultiplier : 1f;
        Vector2D delta = normalized * (_player.EffectiveMoveSpeed * terrainFactor * deltaTime);

        _player.Move(normalized, 0f);
        _collisionDetector.MoveWithTileCollision(_player, delta, _player.Size, _level);
    }

    /// <summary>Adds a harvestable resource node to the active island (or hibernates it if its chunk is unloaded).</summary>
    public void AddResource(ResourceNode resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (_level.UsesLazyChunkLoading)
        {
            ChunkCoord coord = _level.GetChunkCoordForWorld(resource.Center);
            if (!_level.IsChunkLoaded(coord))
            {
                _chunkManager.HibernateResource(coord, resource);
                return;
            }
        }

        ActivateChunkResource(resource);
    }

    /// <summary>Places a structure onto the island and updates tile solid occupancy (hibernating if its chunk is unloaded).</summary>
    public void AddStructure(Structure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        if (structure.IsSolid && _level.IsInBounds(structure.TileCol, structure.TileRow))
        {
            _level.SetStructureOccupancy(structure.TileCol, structure.TileRow, true);
        }

        if (_level.UsesLazyChunkLoading)
        {
            ChunkCoord coord = _level.GetChunkCoordForTile(structure.TileCol, structure.TileRow);
            if (!_level.IsChunkLoaded(coord))
            {
                _chunkManager.HibernateStructure(coord, structure);
                return;
            }
        }

        ActivateChunkStructure(structure);
        RequestSound("build");
    }

    /// <summary>Finds the nearest living chest structure with an inventory within <paramref name="maxDistance"/>.</summary>
    public Structure? FindNearestChest(Vector2D worldPosition, float maxDistance = 88f)
    {
        Structure? best = null;
        float bestDistSq = maxDistance * maxDistance;
        for (int i = 0; i < _structures.Count; i++)
        {
            Structure s = _structures[i];
            if (!s.IsAlive || !s.HasInventory)
            {
                continue;
            }

            float distSq = Vector2D.DistanceSquared(worldPosition, s.Center);
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                best = s;
            }
        }

        return best;
    }

    /// <summary>Atomically deposits <paramref name="quantity"/> units from player inventory into <paramref name="chest"/>.</summary>
    public bool TryDepositToChest(Structure chest, string itemId, int quantity)
    {
        ArgumentNullException.ThrowIfNull(chest);
        if (!chest.IsAlive || chest.StorageInventory is null || quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!_player.Inventory.HasItem(itemId, quantity))
        {
            return false;
        }

        Item resolved = _itemResolver(itemId);
        if (!chest.StorageInventory.CanAddItem(resolved, quantity))
        {
            return false;
        }

        if (!_player.Inventory.TryRemoveItem(itemId, quantity))
        {
            return false;
        }

        chest.StorageInventory.TryAddItem(resolved, quantity);
        _player.SyncEquippedFromSelectedSlot();
        RequestSound("pickup");
        return true;
    }

    /// <summary>Atomically withdraws <paramref name="quantity"/> units from <paramref name="chest"/> into player inventory.</summary>
    public bool TryWithdrawFromChest(Structure chest, string itemId, int quantity)
    {
        ArgumentNullException.ThrowIfNull(chest);
        if (!chest.IsAlive || chest.StorageInventory is null || quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        if (!chest.StorageInventory.HasItem(itemId, quantity))
        {
            return false;
        }

        Item resolved = _itemResolver(itemId);
        if (!_player.Inventory.CanAddItem(resolved, quantity))
        {
            return false;
        }

        if (!chest.StorageInventory.TryRemoveItem(itemId, quantity))
        {
            return false;
        }

        _player.Inventory.TryAddItem(resolved, quantity);
        _player.SyncEquippedFromSelectedSlot();
        RequestSound("pickup");
        return true;
    }

    /// <summary>Looks up a living structure occupying <paramref name="tile"/> in O(1) time (used by wall-breaching AI).</summary>
    public Structure? FindStructureAtTile(GridPoint tile)
    {
        return _structureByTile.TryGetValue(tile, out Structure? s) && s.IsAlive ? s : null;
    }

    /// <summary>Registers a hostile enemy into the active session.</summary>
    public void AddEnemy(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        _enemies.Add(enemy);
        enemy.OnDefeated += HandleEnemyDefeated;
    }

    /// <summary>Registers a passive wildlife/livestock animal into the session (or hibernates if its chunk is unloaded).</summary>
    public void AddAnimal(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        if (_level.UsesLazyChunkLoading)
        {
            ChunkCoord coord = _level.GetChunkCoordForWorld(animal.Center);
            if (!_level.IsChunkLoaded(coord))
            {
                _chunkManager.HibernateAnimal(coord, animal);
                return;
            }
        }

        ActivateChunkAnimal(animal);
    }

    /// <summary>Spawns and tracks the island's boss.</summary>
    public void SpawnBoss(Boss boss)
    {
        ArgumentNullException.ThrowIfNull(boss);
        _activeBoss = boss;
        _bossDefeated = false;
        boss.OnHealthChanged += HandleBossHealthChanged;
        boss.OnEnraged += HandleBossEnraged;
        AddEnemy(boss);
        NotifyBossHealth(boss.BossName, boss.CurrentHp, boss.MaxHp, isVisible: true);
        RequestSound("boss_spawn");
    }

    /// <summary>Acquires and activates a pooled <see cref="Projectile"/> with zero heap allocation.</summary>
    public Projectile SpawnPooledProjectile(
        Vector2D origin,
        Vector2D velocity,
        int damage,
        string ownerId,
        bool isFromPlayer,
        float lifetime = DEFAULT_PROJECTILE_LIFETIME)
    {
        Projectile projectile = _projectilePool.Acquire();
        projectile.Reset(origin, velocity, damage, lifetime, ownerId, isFromPlayer);
        _projectiles.Add(projectile);
        RequestSound("shoot");
        return projectile;
    }

    /// <summary>Acquires and activates a pooled <see cref="DroppedItem"/> with zero heap allocation.</summary>
    public DroppedItem SpawnPooledDroppedItem(string itemId, int quantity, Vector2D position)
    {
        DroppedItem drop = _droppedItemPool.Acquire();
        drop.Reset(itemId, quantity, position);
        _droppedItems.Add(drop);
        return drop;
    }

    /// <summary>Adds an externally created projectile into the simulation.</summary>
    public void AddProjectile(Projectile projectile)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        _projectiles.Add(projectile);
    }

    /// <summary>Adds an externally created dropped item into the simulation.</summary>
    public void AddDroppedItem(DroppedItem droppedItem)
    {
        ArgumentNullException.ThrowIfNull(droppedItem);
        _droppedItems.Add(droppedItem);
    }

    /// <summary>Updates the current wave counter.</summary>
    public void SetWaveIndex(int waveIndex)
    {
        if (waveIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waveIndex), "Wave index cannot be negative.");
        }

        _currentWaveIndex = waveIndex;
    }

    /// <summary>Checks whether the player is within interaction distance of any active workbench.</summary>
    public bool IsPlayerNearWorkbench(float interactRadius = DEFAULT_WORKBENCH_RADIUS)
    {
        float radiusSquared = interactRadius * interactRadius;
        for (int i = 0; i < _structures.Count; i++)
        {
            Structure structure = _structures[i];
            if (structure.IsAlive && structure.IsCraftingStation &&
                Vector2D.DistanceSquared(_player.Center, structure.Center) <= radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Attempts to craft <paramref name="recipe"/> using the player's inventory and workbench proximity.</summary>
    public bool TryCraft(CraftingRecipe recipe, float workbenchRadius = DEFAULT_WORKBENCH_RADIUS)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        bool nearWorkbench = IsPlayerNearWorkbench(workbenchRadius);

        if (!_craftingSystem.TryCraft(_player.Inventory, recipe, nearWorkbench, out _))
        {
            return false;
        }

        _ruleEngine.RecordCraftCompleted();
        _tutorialTracker.NotifyAction(TutorialTriggerType.CraftItem, recipe.OutputItemId);
        _player.SyncEquippedFromSelectedSlot();
        RequestSound("craft");
        return true;
    }

    /// <summary>Attempts to consume/use the currently selected hotbar item polymorphically.</summary>
    public bool TryUseSelectedHotbarItem()
    {
        ItemStack? selected = _player.SelectedItemStack;
        if (selected is null || !selected.Item.TryUse(_player))
        {
            return false;
        }

        _player.Inventory.TryRemoveItem(selected.Item.Id, 1);
        _player.SyncEquippedFromSelectedSlot();
        RequestSound("potion");
        return true;
    }

    /// <summary>
    /// Executes the player's primary action (ranged shot, melee attack, or resource harvesting)
    /// toward <paramref name="aimWorldPosition"/> inside Core rules so UI contains zero combat math.
    /// </summary>
    public bool ExecutePrimaryAction(Vector2D aimWorldPosition)
    {
        if (CurrentState != GameState.Playing || !_player.CanAct)
        {
            return false;
        }

        Vector2D aimDir = (aimWorldPosition - _player.Center).Normalized();
        if (aimDir != Vector2D.Zero)
        {
            _lastAimDirection = aimDir;
        }

        _swingEffectRemaining = 0.16f;
        if (TryFireEquippedRangedWeapon(_lastAimDirection))
        {
            return true;
        }

        if (TryMeleeStrikeEnemy(aimWorldPosition))
        {
            return true;
        }

        if (TryMeleeStrikeAnimal(aimWorldPosition))
        {
            return true;
        }

        if (TryHarvestMatureFarmPlot(aimWorldPosition))
        {
            return true;
        }

        if (_player.EquippedToolType == ToolType.Hoe && TryTillTileWithHoe(aimWorldPosition))
        {
            return true;
        }

        return TryHarvestResourceNear(aimWorldPosition);
    }

    /// <summary>
    /// Executes the player's secondary action: consumes food/potion, plants seed, or places a structure at <paramref name="aimWorldPosition"/>.
    /// </summary>
    public bool ExecuteSecondaryAction(Vector2D aimWorldPosition, IEntityFactory entityFactory)
    {
        ArgumentNullException.ThrowIfNull(entityFactory);
        if (CurrentState != GameState.Playing || !_player.IsAlive)
        {
            return false;
        }

        if (TryUseSelectedHotbarItem())
        {
            return true;
        }

        if (TryPlantSelectedSeed(aimWorldPosition))
        {
            return true;
        }

        return TryPlaceSelectedStructure(aimWorldPosition, entityFactory);
    }

    /// <summary>
    /// Summons the current island's boss if not yet active or defeated.
    /// </summary>
    public bool TrySummonIslandBoss(BossConfiguration bossConfig, IEntityFactory entityFactory)
    {
        ArgumentNullException.ThrowIfNull(bossConfig);
        ArgumentNullException.ThrowIfNull(entityFactory);

        if (CurrentState != GameState.Playing || _activeBoss is not null || _bossDefeated)
        {
            return false;
        }

        Vector2D spawnPos = FindSafeBossSpawnPosition();
        Boss boss = entityFactory.CreateBoss(bossConfig, spawnPos, _difficulty);
        SpawnBoss(boss);
        return true;
    }

    /// <summary>Snapshots previous positions of moving entities before a fixed physics step for smooth render interpolation.</summary>
    public void CapturePreviousPositions()
    {
        _player.CapturePreviousPosition();

        for (int i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i].IsActive)
            {
                _enemies[i].CapturePreviousPosition();
            }
        }

        for (int i = 0; i < _animals.Count; i++)
        {
            if (_animals[i].IsActive)
            {
                _animals[i].CapturePreviousPosition();
            }
        }

        _settlementManager.CapturePreviousPositions();

        for (int i = 0; i < _projectiles.Count; i++)
        {
            if (_projectiles[i].IsActive)
            {
                _projectiles[i].CapturePreviousPosition();
            }
        }
    }

    /// <summary>Delegates fixed-timestep simulation to the active <see cref="IGameState"/> in <see cref="StateMachine"/>.</summary>
    public void Update(float deltaTime) => _stateMachine.Update(deltaTime);

    /// <summary>Creates a complete serializable <see cref="GameStateDTO"/> for save storage.</summary>
    public GameStateDTO CreateSnapshot()
    {
        var snapshot = new GameStateDTO
        {
            IslandId = _level.IslandId,
            GameMode = _gameMode.ToString(),
            WorldSeed = _worldSeed,
            Difficulty = _difficulty.ToString(),
            PlayTimeSeconds = _elapsedPlayTimeSeconds,
            TutorialStepIndex = _tutorialTracker.CurrentStepIndex,
            Score = _ruleEngine.Score,
            DayNumber = _dayNightCycle.DayNumber,
            TimeOfDaySeconds = _dayNightCycle.ElapsedSecondsInCycle,
            CurrentWaveIndex = _currentWaveIndex,
            BossSpawned = _activeBoss is not null,
            Player = CreatePlayerSnapshot()
        };

        PopulateEntitySnapshots(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Restores 100% of world entities, player position/HP/inventory, wave counter,
    /// score, and day/night cycle from a persisted <see cref="GameStateDTO"/>.
    /// </summary>
    public void RestoreFromSnapshot(GameStateDTO snapshot, IEntityFactory entityFactory)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(entityFactory);

        ClearActiveWorldEntities();
        if (Enum.TryParse(snapshot.GameMode, ignoreCase: true, out GameMode restoredMode))
        {
            _gameMode = restoredMode;
        }
        else if (snapshot.IslandId.StartsWith("ENDLESS_", StringComparison.OrdinalIgnoreCase))
        {
            _gameMode = GameMode.Endless;
        }

        _worldSeed = snapshot.WorldSeed;
        if (Enum.TryParse(snapshot.Difficulty, ignoreCase: true, out Difficulty restoredDifficulty))
        {
            _difficulty = restoredDifficulty;
            _difficultyProfile = _difficultyCatalog.GetProfile(_difficulty);
            _player.SetDamageMitigationStrategy(
                new DifficultyDamageMitigationDecorator(FlatDefenseMitigationStrategy.Instance, _difficultyProfile));
            _tutorialTracker.SetEnabled(_difficultyProfile.EnableTutorialGuide);
        }

        _elapsedPlayTimeSeconds = snapshot.PlayTimeSeconds >= 0.0 ? snapshot.PlayTimeSeconds : 0.0;
        _tutorialTracker.RestoreProgress(snapshot.TutorialStepIndex);

        RestorePlayerFromSnapshot(snapshot.Player);
        _ruleEngine.RestoreScore(snapshot.Score);
        _dayNightCycle.RestoreState(snapshot.DayNumber, snapshot.TimeOfDaySeconds, suppressEvents: true);
        NotifyDayPhase(force: true);
        _currentWaveIndex = snapshot.CurrentWaveIndex;
        _waveSpawner?.RestoreWaveIndex(snapshot.CurrentWaveIndex);

        if (snapshot.GeneratedChunks is { Count: > 0 })
        {
            var generated = new List<ChunkCoord>(snapshot.GeneratedChunks.Count);
            for (int i = 0; i < snapshot.GeneratedChunks.Count; i++)
            {
                generated.Add(new ChunkCoord(snapshot.GeneratedChunks[i].ChunkX, snapshot.GeneratedChunks[i].ChunkY));
            }

            _chunkManager.RestoreGeneratedChunks(generated);
        }
        else if (_level.UsesLazyChunkLoading && snapshot.Resources.Count > 0)
        {
            var inferred = new HashSet<ChunkCoord>();
            for (int i = 0; i < snapshot.Resources.Count; i++)
            {
                inferred.Add(_level.GetChunkCoordForWorld(new Vector2D(snapshot.Resources[i].X, snapshot.Resources[i].Y)));
            }

            _chunkManager.RestoreGeneratedChunks(inferred);
        }

        if (_level.UsesLazyChunkLoading)
        {
            InitializeChunksAroundPlayer();
        }

        RestoreEntitiesFromSnapshot(snapshot, entityFactory);
    }

    /// <summary>Cleanly unsubscribes internal event handlers to prevent memory leaks.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _player.OnHealthChanged -= HandlePlayerHealthChanged;
        _player.Inventory.OnInventoryChanged -= HandleInventoryChanged;
        _craftingSystem.OnItemCrafted -= HandleItemCrafted;
        _ruleEngine.OnScoreChanged -= HandleScoreChanged;
        _dayNightCycle.OnDayPhaseChanged -= HandleDayPhaseChanged;
        _stateMachine.OnStateChanged -= HandleStateChanged;
        ClearActiveWorldEntities();
        _hudObservers.Clear();
        _disposed = true;
    }

    private void ConfigureStateMachine()
    {
        _stateMachine.RegisterState(new MainMenuState());
        _stateMachine.RegisterState(new PlayingState(StepPlayingSimulation));
        _stateMachine.RegisterState(new PausedState());
        _stateMachine.RegisterState(new VictoryState(() => RequestSound("victory")));
        _stateMachine.RegisterState(new GameOverState(() => RequestSound("game_over")));
        _stateMachine.TransitionTo(GameState.MainMenu);
    }

    private void SubscribeInternalEvents()
    {
        _player.OnHealthChanged += HandlePlayerHealthChanged;
        _player.Inventory.OnInventoryChanged += HandleInventoryChanged;
        _craftingSystem.OnItemCrafted += HandleItemCrafted;
        _ruleEngine.OnScoreChanged += HandleScoreChanged;
        _dayNightCycle.OnDayPhaseChanged += HandleDayPhaseChanged;
        _stateMachine.OnStateChanged += HandleStateChanged;
    }

    private void StepPlayingSimulation(float deltaTime)
    {
        _elapsedPlayTimeSeconds += deltaTime;
        if (_swingEffectRemaining > 0f)
        {
            _swingEffectRemaining = MathF.Max(0f, _swingEffectRemaining - deltaTime);
        }

        _pathfindingCoordinator.BeginTick(deltaTime);
        _chunkManager.UpdateAround(
            _player.Center,
            _onActivateChunkResource,
            _onActivateChunkStructure,
            _onDeactivateChunkEntities,
            onActivateAnimal: _onActivateChunkAnimal,
            onActivateSettler: _onActivateChunkSettler);
        _dayNightCycle.Advance(deltaTime);
        NotifyDayPhase();
        UpdateNightReinforcements(deltaTime);
        _player.Update(deltaTime);
        _farmingSystem.Update(deltaTime);
        UpdateAnimals(deltaTime);
        _settlementManager.Update(
            deltaTime,
            _player,
            _level,
            _enemiesView,
            _resourcesView,
            _structuresView,
            _structureLookup,
            _dayNightCycle.CurrentPhase);
        UpdateEnemies(deltaTime);
        RebuildSpatialGrids();
        UpdateProjectiles(deltaTime);
        UpdateDroppedItems(deltaTime);
        CleanupInactiveEntities();
        EvaluateSessionOutcome();
    }

    private bool TryFireEquippedRangedWeapon(Vector2D aimDirection)
    {
        IWeapon? weapon = _player.EffectiveEquippedWeapon;
        if (weapon is null || !weapon.IsRanged || aimDirection == Vector2D.Zero)
        {
            return false;
        }

        SpawnPooledProjectile(_player.Center, aimDirection * weapon.ProjectileSpeed, weapon.ComputeDamage(), _player.Id, isFromPlayer: true);
        _player.TriggerActionCooldown(weapon.ComputeCooldown());
        return true;
    }

    private bool TryMeleeStrikeEnemy(Vector2D aimWorldPosition)
    {
        float reach = _player.EquippedWeapon?.AttackRange ?? 54f;
        Enemy? bestTarget = FindClosestEnemyInReach(aimWorldPosition, reach);
        if (bestTarget is null)
        {
            return false;
        }

        bool struck = _ruleEngine.ResolveMeleeAttack(_player, bestTarget);
        if (struck)
        {
            _tutorialTracker.NotifyAction(TutorialTriggerType.DamageEnemy, bestTarget.Archetype);
            RequestSound("hit");
        }

        return struck;
    }

    private Enemy? FindClosestEnemyInReach(Vector2D aimWorldPosition, float reach)
    {
        Enemy? best = null;
        float bestAimDist = float.MaxValue;
        float reachSq = (reach + 16f) * (reach + 16f);

        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy enemy = _enemies[i];
            if (!enemy.IsAlive || Vector2D.DistanceSquared(_player.Center, enemy.Center) > reachSq)
            {
                continue;
            }

            float aimDist = Vector2D.DistanceSquared(aimWorldPosition, enemy.Center);
            if (aimDist < bestAimDist)
            {
                bestAimDist = aimDist;
                best = enemy;
            }
        }

        return best;
    }

    private bool TryMeleeStrikeAnimal(Vector2D aimWorldPosition)
    {
        float reach = _player.EquippedWeapon?.AttackRange ?? 54f;
        Animal? bestTarget = FindClosestAnimalInReach(aimWorldPosition, reach);
        if (bestTarget is null)
        {
            return false;
        }

        bool struck = _ruleEngine.ResolveMeleeAttack(_player, bestTarget);
        if (struck)
        {
            RequestSound("hit");
        }

        return struck;
    }

    private Animal? FindClosestAnimalInReach(Vector2D aimWorldPosition, float reach)
    {
        Animal? best = null;
        float bestAimDist = float.MaxValue;
        float reachSq = (reach + 16f) * (reach + 16f);

        for (int i = 0; i < _animals.Count; i++)
        {
            Animal animal = _animals[i];
            if (!animal.IsAlive || Vector2D.DistanceSquared(_player.Center, animal.Center) > reachSq)
            {
                continue;
            }

            float aimDist = Vector2D.DistanceSquared(aimWorldPosition, animal.Center);
            if (aimDist < bestAimDist)
            {
                bestAimDist = aimDist;
                best = animal;
            }
        }

        return best;
    }

    private bool TryHarvestResourceNear(Vector2D aimWorldPosition)
    {
        const float HARVEST_REACH_SQ = 68f * 68f;
        ResourceNode? bestNode = null;
        float bestAimDist = float.MaxValue;

        for (int i = 0; i < _resources.Count; i++)
        {
            ResourceNode node = _resources[i];
            if (!node.IsAlive || Vector2D.DistanceSquared(_player.Center, node.Center) > HARVEST_REACH_SQ)
            {
                continue;
            }

            float aimDist = Vector2D.DistanceSquared(aimWorldPosition, node.Center);
            if (aimDist < bestAimDist)
            {
                bestAimDist = aimDist;
                bestNode = node;
            }
        }

        if (bestNode is null)
        {
            _player.TriggerActionCooldown();
            RequestSound("swing");
            return false;
        }

        if (_ruleEngine.TryResolveHarvest(
                bestNode,
                _player.EquippedToolType,
                _player.HarvestPower,
                out string dropItemId,
                out int dropQuantity,
                out Vector2D dropPosition))
        {
            _tutorialTracker.NotifyAction(TutorialTriggerType.HarvestResource, bestNode.ResourceType.ToString());
            SpawnPooledDroppedItem(dropItemId, dropQuantity, dropPosition);
        }

        _player.TriggerActionCooldown();
        RequestSound("harvest");
        return true;
    }

    private bool TryHarvestMatureFarmPlot(Vector2D aimWorldPosition)
    {
        if (Vector2D.Distance(_player.Center, aimWorldPosition) > 68f)
        {
            return false;
        }

        (int col, int row) = _level.WorldToTile(aimWorldPosition);
        if (!_farmingSystem.TryHarvestPlot(
                col,
                row,
                _gameplayRng,
                out string harvestItemId,
                out int harvestQuantity,
                out string? bonusSeedItemId))
        {
            return false;
        }

        Vector2D dropPos = _level.TileToWorldCenter(col, row);
        SpawnPooledDroppedItem(harvestItemId, harvestQuantity, dropPos);
        if (!string.IsNullOrWhiteSpace(bonusSeedItemId))
        {
            SpawnPooledDroppedItem(bonusSeedItemId, 1, dropPos + new Vector2D(6f, 6f));
        }

        _ruleEngine.AddScore(RuleEngine.HARVEST_SCORE);
        _player.TriggerActionCooldown();
        RequestSound("harvest");
        return true;
    }

    private bool TryTillTileWithHoe(Vector2D aimWorldPosition)
    {
        if (Vector2D.Distance(_player.Center, aimWorldPosition) > 96f)
        {
            return false;
        }

        (int col, int row) = _level.WorldToTile(aimWorldPosition);
        if (!_farmingSystem.TryTillTile(_level, col, row, FindStructureAtTile))
        {
            return false;
        }

        _player.TriggerActionCooldown(0.30f);
        RequestSound("harvest");
        return true;
    }

    private bool TryPlantSelectedSeed(Vector2D aimWorldPosition)
    {
        ItemStack? selected = _player.SelectedItemStack;
        if (selected is null || !selected.Item.TryGetPlantableCropId(out string cropId))
        {
            return false;
        }

        if (Vector2D.Distance(_player.Center, aimWorldPosition) > 112f)
        {
            return false;
        }

        (int col, int row) = _level.WorldToTile(aimWorldPosition);
        if (!_farmingSystem.TryPlantSeed(col, row, cropId))
        {
            return false;
        }

        _player.Inventory.TryRemoveItem(selected.Item.Id, 1);
        _player.SyncEquippedFromSelectedSlot();
        RequestSound("build");
        return true;
    }

    private bool TryPlaceSelectedStructure(Vector2D aimWorldPosition, IEntityFactory entityFactory)
    {
        ItemStack? selected = _player.SelectedItemStack;
        if (selected is null || !selected.Item.TryGetPlaceableStructure(out StructureType structureType))
        {
            return false;
        }

        if (Vector2D.Distance(_player.Center, aimWorldPosition) > 112f)
        {
            return false;
        }

        (int col, int row) = _level.WorldToTile(aimWorldPosition);
        if (!_level.IsTileWalkable(col, row) || FindStructureAtTile(new GridPoint(col, row)) is not null)
        {
            return false;
        }

        RectF tileBounds = _level.GetTile(col, row).GetWorldBounds(_level.TileSize);
        if (tileBounds.Intersects(_player.GetBounds()))
        {
            return false;
        }

        Structure structure = entityFactory.CreateStructure(structureType, col, row, _level.TileSize);
        AddStructure(structure);
        _tutorialTracker.NotifyAction(TutorialTriggerType.PlaceStructure, structureType.ToString());
        _player.Inventory.TryRemoveItem(selected.Item.Id, 1);
        _player.SyncEquippedFromSelectedSlot();
        return true;
    }

    private Vector2D FindSafeBossSpawnPosition()
    {
        (int pCol, int pRow) = _level.WorldToTile(_player.Center);
        for (int radius = 6; radius <= 14; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (global::System.Math.Abs(dx) != radius && global::System.Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    int col = pCol + dx;
                    int row = pRow + dy;
                    if (!_level.IsTileWalkable(col, row))
                    {
                        continue;
                    }

                    Vector2D candidate = _level.TileToWorldCenter(col, row);
                    float dist = Vector2D.Distance(candidate, _player.Center);
                    if (dist >= 180f && dist <= 448f)
                    {
                        return candidate;
                    }
                }
            }
        }

        IReadOnlyList<ChunkCoord> loadedCoords = _level.LoadedChunkCoords;
        for (int idx = 0; idx < loadedCoords.Count; idx++)
        {
            if (!_level.TryGetLoadedChunk(loadedCoords[idx], out WorldChunk? chunk) || chunk is null)
            {
                continue;
            }

            for (int r = 0; r < chunk.Height; r++)
            {
                for (int c = 0; c < chunk.Width; c++)
                {
                    Tile tile = chunk.GetLocalTile(c, r);
                    if (!tile.IsWalkable)
                    {
                        continue;
                    }

                    Vector2D candidate = _level.TileToWorldCenter(tile.Col, tile.Row);
                    if (Vector2D.Distance(candidate, _player.Center) >= 160f)
                    {
                        return candidate;
                    }
                }
            }
        }

        return _level.PlayerSpawnWorldPosition;
    }

    private void RebuildSpatialGrids()
    {
        _playerTargetGrid.Clear();
        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy enemy = _enemies[i];
            if (!enemy.IsAlive)
            {
                continue;
            }

            if (!enemy.IsBoss && _level.UsesLazyChunkLoading && !_level.IsChunkLoaded(_level.GetChunkCoordForWorld(enemy.Center)))
            {
                continue;
            }

            _playerTargetGrid.Insert(enemy);
        }

        for (int i = 0; i < _animals.Count; i++)
        {
            Animal animal = _animals[i];
            if (!animal.IsAlive)
            {
                continue;
            }

            if (_level.UsesLazyChunkLoading && !_level.IsChunkLoaded(_level.GetChunkCoordForWorld(animal.Center)))
            {
                continue;
            }

            _playerTargetGrid.Insert(animal);
        }

        _playerSpatialGrid.Clear();
        if (_player.IsAlive)
        {
            _playerSpatialGrid.Insert(_player);
        }
    }

    private void UpdateAnimals(float deltaTime)
    {
        for (int i = 0; i < _animals.Count; i++)
        {
            Animal animal = _animals[i];
            if (!animal.IsAlive)
            {
                continue;
            }

            if (_level.UsesLazyChunkLoading && !_level.IsChunkLoaded(_level.GetChunkCoordForWorld(animal.Center)))
            {
                continue;
            }

            animal.Update(deltaTime);
            animal.UpdateBehavior(_player, _level, _collisionDetector, deltaTime);
        }
    }

    private void UpdateEnemies(float deltaTime)
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy enemy = _enemies[i];
            if (!enemy.IsAlive)
            {
                continue;
            }

            if (!enemy.IsBoss && _level.UsesLazyChunkLoading && !_level.IsChunkLoaded(_level.GetChunkCoordForWorld(enemy.Center)))
            {
                continue;
            }

            enemy.Update(deltaTime);
            enemy.ExecuteAI(_player, _level, deltaTime);
        }
    }

    private void UpdateProjectiles(float deltaTime)
    {
        for (int i = 0; i < _projectiles.Count; i++)
        {
            Projectile projectile = _projectiles[i];
            if (!projectile.IsActive)
            {
                continue;
            }

            if (projectile.IsFromPlayer)
            {
                StepPlayerProjectile(projectile, deltaTime);
            }
            else
            {
                StepEnemyProjectile(projectile, deltaTime);
            }
        }
    }

    private void StepPlayerProjectile(Projectile projectile, float deltaTime)
    {
        bool hit = _collisionDetector.TryAdvanceProjectileSwept(
            projectile,
            deltaTime,
            _level,
            _playerTargetGrid,
            _characterQueryBuffer,
            out Character? hitTarget,
            out _);

        if (hit && hitTarget is not null)
        {
            _ruleEngine.ResolveProjectileHit(projectile, hitTarget);
            if (hitTarget is Enemy hitEnemy)
            {
                _tutorialTracker.NotifyAction(TutorialTriggerType.DamageEnemy, hitEnemy.Archetype);
            }

            RequestSound("hit");
        }
    }

    private void StepEnemyProjectile(Projectile projectile, float deltaTime)
    {
        bool hit = _collisionDetector.TryAdvanceProjectileSwept(
            projectile,
            deltaTime,
            _level,
            _playerSpatialGrid,
            _playerQueryBuffer,
            out Player? hitPlayer,
            out _);

        if (hit && hitPlayer is not null)
        {
            _ruleEngine.ResolveProjectileHit(projectile, hitPlayer);
            RequestSound("player_hurt");
        }
    }

    private void UpdateDroppedItems(float deltaTime)
    {
        float pickupRadiusSq = DEFAULT_PICKUP_RADIUS * DEFAULT_PICKUP_RADIUS;
        for (int i = 0; i < _droppedItems.Count; i++)
        {
            DroppedItem drop = _droppedItems[i];
            if (!drop.IsActive)
            {
                continue;
            }

            drop.Update(deltaTime);
            if (drop.IsActive &&
                Vector2D.DistanceSquared(_player.Center, drop.Center) <= pickupRadiusSq &&
                _ruleEngine.ResolvePickup(drop, _player.Inventory, _itemResolver))
            {
                RequestSound("pickup");
            }
        }
    }

    private void CleanupInactiveEntities()
    {
        ReleaseInactiveProjectiles();
        ReleaseInactiveDrops();
        RemoveInactiveInPlace(_resources);
        RemoveInactiveStructuresInPlace();
        RemoveInactiveInPlace(_enemies);
        RemoveInactiveInPlace(_animals);
    }

    private void RemoveInactiveStructuresInPlace()
    {
        for (int i = _structures.Count - 1; i >= 0; i--)
        {
            Structure structure = _structures[i];
            if (!structure.IsActive)
            {
                structure.OnDefeated -= HandleStructureDestroyed;
                _structureByTile.Remove(new GridPoint(structure.TileCol, structure.TileRow));
                _structures.RemoveAt(i);
            }
        }
    }

    private void ReleaseInactiveProjectiles()
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            if (!_projectiles[i].IsActive)
            {
                Projectile inactive = _projectiles[i];
                _projectiles.RemoveAt(i);
                _projectilePool.Release(inactive);
            }
        }
    }

    private void ReleaseInactiveDrops()
    {
        for (int i = _droppedItems.Count - 1; i >= 0; i--)
        {
            if (!_droppedItems[i].IsActive)
            {
                DroppedItem inactive = _droppedItems[i];
                _droppedItems.RemoveAt(i);
                _droppedItemPool.Release(inactive);
            }
        }
    }

    private static void RemoveInactiveInPlace<T>(List<T> entities) where T : Entity
    {
        for (int i = entities.Count - 1; i >= 0; i--)
        {
            if (!entities[i].IsActive)
            {
                entities.RemoveAt(i);
            }
        }
    }

    private void EvaluateSessionOutcome()
    {
        GameState outcome = _ruleEngine.EvaluateOutcome(_player, _bossDefeated);
        if (outcome != CurrentState)
        {
            _stateMachine.TransitionTo(outcome);
        }
    }

    private void HandleStateChanged(GameState newState)
    {
        OnGameStateChanged?.Invoke(newState);
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnGameStateUpdated(newState);
        }
    }

    private void HandlePlayerHealthChanged(int currentHp, int maxHp)
    {
        OnHealthChanged?.Invoke(currentHp, maxHp);
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnPlayerHealthUpdated(currentHp, maxHp);
        }
    }

    private void HandleInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnInventoryUpdated();
        }
    }

    private void HandleItemCrafted(CraftingRecipe recipe, ItemStack stack) => OnItemCrafted?.Invoke(recipe, stack);

    private void HandleScoreChanged(int newScore)
    {
        OnScoreChanged?.Invoke(newScore);
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnScoreUpdated(newScore);
        }
    }

    private void HandleDayPhaseChanged(int dayNumber, DayPhase phase)
    {
        NotifyDayPhase(force: true);
        if (phase == DayPhase.Night && _waveSpawner is not null)
        {
            _nightReinforcementTimer = NIGHT_REINFORCEMENT_INTERVAL;
            DispatchNightWave();
        }
    }

    private void UpdateNightReinforcements(float deltaTime)
    {
        if (_waveSpawner is null || _dayNightCycle.CurrentPhase != DayPhase.Night)
        {
            return;
        }

        _nightReinforcementTimer -= deltaTime;
        if (_nightReinforcementTimer <= 0f)
        {
            _nightReinforcementTimer = NIGHT_REINFORCEMENT_INTERVAL;
            if (_enemies.Count < MAX_ACTIVE_NIGHT_ENEMIES)
            {
                DispatchNightWave();
            }
        }
    }

    private void DispatchNightWave()
    {
        if (_waveSpawner is null) return;

        int spawned = _waveSpawner.SpawnNightSkirmishWave(_player, _waveSpawnBuffer);
        for (int i = 0; i < spawned && _enemies.Count < MAX_ACTIVE_NIGHT_ENEMIES; i++)
        {
            AddEnemy(_waveSpawnBuffer[i]);
        }

        _currentWaveIndex = _waveSpawner.NextWaveIndex;
        RequestSound("raid_horn");
    }

    private void NotifyDayPhase(bool force = false)
    {
        int day = _dayNightCycle.DayNumber;
        DayPhase phase = _dayNightCycle.CurrentPhase;
        float progress = _dayNightCycle.NormalizedPhaseProgress;
        int progressBucket = (int)(progress * 100f);

        if (!force &&
            day == _lastNotifiedDay &&
            phase == _lastNotifiedPhase &&
            progressBucket == _lastNotifiedProgressBucket)
        {
            return;
        }

        _lastNotifiedDay = day;
        _lastNotifiedPhase = phase;
        _lastNotifiedProgressBucket = progressBucket;

        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnDayPhaseUpdated(day, phase, progress);
        }
    }

    private void HandleEnemyDefeated(IDamageable defeated)
    {
        defeated.OnDefeated -= HandleEnemyDefeated;
        Enemy? matched = FindEnemyByReference(defeated);
        if (matched is null)
        {
            return;
        }

        _ruleEngine.ResolveEnemyDefeated(matched);
        RequestSound("enemy_defeated");

        if (_lootTableRegistry.TryGetTable(matched.Archetype, out _))
        {
            if (_lootTableRegistry.RollDrops(matched.Archetype, _gameplayRng, _lootRollBuffer))
            {
                for (int i = 0; i < _lootRollBuffer.Count; i++)
                {
                    LootDropResult drop = _lootRollBuffer[i];
                    var offset = new Vector2D((i % 3) * 6f - 6f, (i / 3) * 6f);
                    SpawnPooledDroppedItem(drop.ItemId, drop.Quantity, matched.Center + offset);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(matched.LootDropItemId) && matched.LootDropQuantity > 0)
        {
            SpawnPooledDroppedItem(matched.LootDropItemId, matched.LootDropQuantity, matched.Center);
        }

        if (matched.IsBoss)
        {
            _bossDefeated = true;
            if (_activeBoss is not null)
            {
                _activeBoss.OnHealthChanged -= HandleBossHealthChanged;
                _activeBoss.OnEnraged -= HandleBossEnraged;
                NotifyBossHealth(_activeBoss.BossName, 0, _activeBoss.MaxHp, isVisible: false);
            }
        }
    }

    private void HandleBossHealthChanged(int currentHp, int maxHp)
    {
        if (_activeBoss is not null)
        {
            NotifyBossHealth(_activeBoss.BossName, currentHp, maxHp, isVisible: currentHp > 0);
        }
    }

    private void HandleBossEnraged(Boss boss) => RequestSound("boss_enrage");

    private void HandleStructureDestroyed(IDamageable destroyed)
    {
        destroyed.OnDefeated -= HandleStructureDestroyed;
        for (int i = 0; i < _structures.Count; i++)
        {
            if (ReferenceEquals(_structures[i], destroyed))
            {
                Structure structure = _structures[i];
                _structureByTile.Remove(new GridPoint(structure.TileCol, structure.TileRow));
                if (structure.IsSolid && _level.IsInBounds(structure.TileCol, structure.TileRow))
                {
                    _level.SetStructureOccupancy(structure.TileCol, structure.TileRow, false);
                }

                if (structure.StorageInventory is { UsedSlots: > 0 } chestInv)
                {
                    for (int s = 0; s < chestInv.Stacks.Count; s++)
                    {
                        ItemStack stack = chestInv.Stacks[s];
                        var offset = new Vector2D((s % 4) * 5f - 7f, (s / 4) * 5f);
                        SpawnPooledDroppedItem(stack.Item.Id, stack.Quantity, structure.Center + offset);
                    }

                    chestInv.Clear();
                }

                RequestSound("structure_break");
                return;
            }
        }
    }

    private void ActivateChunkResource(ResourceNode resource)
    {
        if (resource.IsAlive)
        {
            _resources.Add(resource);
        }
    }

    private void ActivateChunkStructure(Structure structure)
    {
        if (!structure.IsAlive)
        {
            return;
        }

        var tilePoint = new GridPoint(structure.TileCol, structure.TileRow);
        if (_structureByTile.TryGetValue(tilePoint, out Structure? existing) && existing.IsAlive && !ReferenceEquals(existing, structure))
        {
            return;
        }

        _structures.Add(structure);
        _structureByTile[tilePoint] = structure;
        if (structure.IsSolid && _level.IsInBounds(structure.TileCol, structure.TileRow))
        {
            _level.SetStructureOccupancy(structure.TileCol, structure.TileRow, true);
        }

        structure.OnDefeated -= HandleStructureDestroyed;
        structure.OnDefeated += HandleStructureDestroyed;
    }

    private void ActivateChunkAnimal(Animal animal)
    {
        if (!animal.IsAlive)
        {
            return;
        }

        _animals.Add(animal);
        animal.OnDefeated -= HandleAnimalDefeated;
        animal.OnDefeated += HandleAnimalDefeated;
    }

    private void ActivateChunkSettler(Settler settler)
    {
        if (settler.IsAlive)
        {
            _settlementManager.AddActiveSettler(settler);
        }
    }

    private void HandleAnimalDefeated(IDamageable defeated)
    {
        defeated.OnDefeated -= HandleAnimalDefeated;
        Animal? matched = FindAnimalByReference(defeated);
        if (matched is null)
        {
            return;
        }

        if (matched.ScoreReward > 0)
        {
            _ruleEngine.AddScore(matched.ScoreReward);
        }

        RequestSound("harvest");
        SpawnPooledDroppedItem(matched.PrimaryDropItemId, matched.PrimaryDropQuantity, matched.Center);
        if (!string.IsNullOrWhiteSpace(matched.SecondaryDropItemId) && matched.SecondaryDropQuantity > 0)
        {
            SpawnPooledDroppedItem(matched.SecondaryDropItemId, matched.SecondaryDropQuantity, matched.Center + new Vector2D(8f, 4f));
        }
    }

    private Animal? FindAnimalByReference(IDamageable target)
    {
        for (int i = 0; i < _animals.Count; i++)
        {
            if (ReferenceEquals(_animals[i], target))
            {
                return _animals[i];
            }
        }

        return null;
    }

    private void DeactivateChunkEntities(ChunkCoord coord)
    {
        for (int i = _resources.Count - 1; i >= 0; i--)
        {
            ResourceNode resource = _resources[i];
            if (!resource.IsAlive)
            {
                _resources.RemoveAt(i);
                continue;
            }

            if (_level.GetChunkCoordForWorld(resource.Center) == coord)
            {
                _chunkManager.HibernateResource(coord, resource);
                _resources.RemoveAt(i);
            }
        }

        for (int i = _structures.Count - 1; i >= 0; i--)
        {
            Structure structure = _structures[i];
            if (!structure.IsAlive)
            {
                structure.OnDefeated -= HandleStructureDestroyed;
                _structureByTile.Remove(new GridPoint(structure.TileCol, structure.TileRow));
                _structures.RemoveAt(i);
                continue;
            }

            if (_level.GetChunkCoordForTile(structure.TileCol, structure.TileRow) == coord)
            {
                structure.OnDefeated -= HandleStructureDestroyed;
                _chunkManager.HibernateStructure(coord, structure);
                _structureByTile.Remove(new GridPoint(structure.TileCol, structure.TileRow));
                _structures.RemoveAt(i);
            }
        }

        for (int i = _animals.Count - 1; i >= 0; i--)
        {
            Animal animal = _animals[i];
            if (!animal.IsAlive)
            {
                animal.OnDefeated -= HandleAnimalDefeated;
                _animals.RemoveAt(i);
                continue;
            }

            if (_level.GetChunkCoordForWorld(animal.Center) == coord)
            {
                animal.OnDefeated -= HandleAnimalDefeated;
                _chunkManager.HibernateAnimal(coord, animal);
                _animals.RemoveAt(i);
            }
        }

        IReadOnlyList<Settler> activeSettlers = _settlementManager.Settlers;
        for (int i = activeSettlers.Count - 1; i >= 0; i--)
        {
            Settler settler = activeSettlers[i];
            if (!settler.IsAlive)
            {
                _settlementManager.RemoveActiveSettlerAt(i);
                continue;
            }

            if (_level.GetChunkCoordForWorld(settler.Center) == coord)
            {
                _chunkManager.HibernateSettler(coord, settler);
                _settlementManager.RemoveActiveSettlerAt(i);
            }
        }
    }

    private Enemy? FindEnemyByReference(IDamageable target)
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            if (ReferenceEquals(_enemies[i], target))
            {
                return _enemies[i];
            }
        }

        return null;
    }

    private void RequestSound(string soundKey) => OnSoundEffectRequested?.Invoke(soundKey);

    private Projectile CreatePooledProjectile()
    {
        _pooledProjectileSequence++;
        var projectile = new Projectile($"proj_{_pooledProjectileSequence}", Vector2D.Zero, Vector2D.UnitX, 1, 1f, "pool", true);
        projectile.Deactivate();
        return projectile;
    }

    private DroppedItem CreatePooledDrop()
    {
        _pooledDropSequence++;
        var drop = new DroppedItem($"drop_pool_{_pooledDropSequence}", "wood", 1, Vector2D.Zero);
        drop.Deactivate();
        return drop;
    }

    private void NotifyBossHealth(string bossName, int currentHp, int maxHp, bool isVisible)
    {
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnBossHealthUpdated(bossName, currentHp, maxHp, isVisible);
        }
    }

    private void PushFullStateToObserver(IHudObserver observer)
    {
        observer.OnPlayerHealthUpdated(_player.CurrentHp, _player.MaxHp);
        observer.OnScoreUpdated(_ruleEngine.Score);
        observer.OnInventoryUpdated();
        observer.OnDayPhaseUpdated(_dayNightCycle.DayNumber, _dayNightCycle.CurrentPhase, _dayNightCycle.NormalizedPhaseProgress);
        observer.OnGameStateUpdated(CurrentState);
        if (_activeBoss is not null && _activeBoss.IsAlive)
        {
            observer.OnBossHealthUpdated(_activeBoss.BossName, _activeBoss.CurrentHp, _activeBoss.MaxHp, isVisible: true);
        }
    }

    private PlayerSnapshot CreatePlayerSnapshot()
    {
        var playerSnapshot = new PlayerSnapshot
        {
            X = _player.Position.X,
            Y = _player.Position.Y,
            Hp = _player.CurrentHp,
            MaxHp = _player.MaxHp,
            Hunger = _player.CurrentHunger,
            MaxHunger = _player.MaxHunger,
            SelectedHotbarIndex = _player.SelectedHotbarIndex,
            HeadArmorItemId = _player.Equipment.Head?.Id,
            BodyArmorItemId = _player.Equipment.Body?.Id,
            LegsArmorItemId = _player.Equipment.Legs?.Id
        };

        for (int i = 0; i < _player.Inventory.Stacks.Count; i++)
        {
            ItemStack stack = _player.Inventory.Stacks[i];
            playerSnapshot.Inventory.Add(new InventoryItemSnapshot
            {
                ItemId = stack.Item.Id,
                Quantity = stack.Quantity
            });
        }

        return playerSnapshot;
    }

    private void PopulateEntitySnapshots(GameStateDTO snapshot)
    {
        for (int i = 0; i < _resources.Count; i++)
        {
            ResourceNode r = _resources[i];
            snapshot.Resources.Add(new EntityStateSnapshot { Id = r.Id, Type = r.ResourceType.ToString(), X = r.Position.X, Y = r.Position.Y, Hp = r.CurrentHp });
        }

        foreach (ResourceNode r in _chunkManager.GetHibernatedResources())
        {
            snapshot.Resources.Add(new EntityStateSnapshot { Id = r.Id, Type = r.ResourceType.ToString(), X = r.Position.X, Y = r.Position.Y, Hp = r.CurrentHp });
        }

        for (int i = 0; i < _structures.Count; i++)
        {
            snapshot.Structures.Add(BuildStructureSnapshot(_structures[i]));
        }

        foreach (Structure s in _chunkManager.GetHibernatedStructures())
        {
            snapshot.Structures.Add(BuildStructureSnapshot(s));
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy e = _enemies[i];
            snapshot.Enemies.Add(new EnemyStateSnapshot { Id = e.Id, Type = e.Archetype, X = e.Position.X, Y = e.Position.Y, Hp = e.CurrentHp, IsBoss = e.IsBoss });
        }

        for (int i = 0; i < _animals.Count; i++)
        {
            Animal a = _animals[i];
            snapshot.Animals.Add(new AnimalStateSnapshot { Id = a.Id, Type = a.AnimalType.ToString(), X = a.Position.X, Y = a.Position.Y, Hp = a.CurrentHp });
        }

        foreach (Animal a in _chunkManager.GetHibernatedAnimals())
        {
            snapshot.Animals.Add(new AnimalStateSnapshot { Id = a.Id, Type = a.AnimalType.ToString(), X = a.Position.X, Y = a.Position.Y, Hp = a.CurrentHp });
        }

        foreach (ChunkCoord coord in _chunkManager.GeneratedChunks)
        {
            snapshot.GeneratedChunks.Add(new ChunkCoordSnapshot { ChunkX = coord.ChunkX, ChunkY = coord.ChunkY });
        }

        for (int i = 0; i < _farmingSystem.Plots.Count; i++)
        {
            FarmPlot plot = _farmingSystem.Plots[i];
            snapshot.FarmPlots.Add(new FarmPlotSnapshot
            {
                TileCol = plot.TileCol,
                TileRow = plot.TileRow,
                CropId = plot.CropId,
                GrowthSeconds = plot.GrowthElapsedSeconds
            });
        }

        IReadOnlyList<Settler> activeSettlers = _settlementManager.Settlers;
        for (int i = 0; i < activeSettlers.Count; i++)
        {
            snapshot.Settlers.Add(BuildSettlerSnapshot(activeSettlers[i]));
        }

        foreach (Settler s in _chunkManager.GetHibernatedSettlers())
        {
            snapshot.Settlers.Add(BuildSettlerSnapshot(s));
        }
    }

    private void ClearActiveWorldEntities()
    {
        for (int i = 0; i < _structures.Count; i++)
        {
            Structure s = _structures[i];
            s.OnDefeated -= HandleStructureDestroyed;
            if (s.IsSolid && _level.IsInBounds(s.TileCol, s.TileRow))
            {
                _level.SetStructureOccupancy(s.TileCol, s.TileRow, false);
            }
        }

        foreach (Structure s in _chunkManager.GetHibernatedStructures())
        {
            if (s.IsSolid && _level.IsInBounds(s.TileCol, s.TileRow))
            {
                _level.SetStructureOccupancy(s.TileCol, s.TileRow, false);
            }
        }

        for (int i = 0; i < _animals.Count; i++)
        {
            _animals[i].OnDefeated -= HandleAnimalDefeated;
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].OnDefeated -= HandleEnemyDefeated;
        }

        if (_activeBoss is not null)
        {
            _activeBoss.OnHealthChanged -= HandleBossHealthChanged;
            _activeBoss.OnEnraged -= HandleBossEnraged;
        }

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = _projectiles[i];
            projectile.Deactivate();
            _projectilePool.Release(projectile);
        }

        for (int i = _droppedItems.Count - 1; i >= 0; i--)
        {
            DroppedItem drop = _droppedItems[i];
            drop.Deactivate();
            _droppedItemPool.Release(drop);
        }

        _resources.Clear();
        _structures.Clear();
        _structureByTile.Clear();
        _enemies.Clear();
        _animals.Clear();
        _projectiles.Clear();
        _droppedItems.Clear();
        _farmingSystem.Clear();
        _settlementManager.Clear();
        _player.Equipment.Clear();
        _chunkManager.ClearHibernatedState();
        _pathfindingCoordinator.ClearCache();
        _chunksInitialized = false;
        _activeBoss = null;
        _bossDefeated = false;
        _lastNotifiedDay = -1;
        _lastNotifiedProgressBucket = -1;
    }

    private void RestorePlayerFromSnapshot(PlayerSnapshot playerDto)
    {
        _player.TeleportTo(new Vector2D(playerDto.X, playerDto.Y));
        _player.RestoreHealth(playerDto.Hp);
        if (playerDto.MaxHunger > 0f)
        {
            _player.RestoreHungerExact(playerDto.Hunger, playerDto.MaxHunger);
        }

        _player.Inventory.Clear();

        for (int i = 0; i < playerDto.Inventory.Count; i++)
        {
            InventoryItemSnapshot entry = playerDto.Inventory[i];
            Item resolved = _itemResolver(entry.ItemId);
            _player.Inventory.TryAddItem(resolved, entry.Quantity);
        }

        ArmorItem? head = !string.IsNullOrWhiteSpace(playerDto.HeadArmorItemId) ? _itemResolver(playerDto.HeadArmorItemId).AsArmorItem() : null;
        ArmorItem? body = !string.IsNullOrWhiteSpace(playerDto.BodyArmorItemId) ? _itemResolver(playerDto.BodyArmorItemId).AsArmorItem() : null;
        ArmorItem? legs = !string.IsNullOrWhiteSpace(playerDto.LegsArmorItemId) ? _itemResolver(playerDto.LegsArmorItemId).AsArmorItem() : null;
        _player.RestoreEquippedArmor(head, body, legs);

        int clampedHotbar = global::System.Math.Clamp(playerDto.SelectedHotbarIndex, 0, _player.HotbarCapacity - 1);
        _player.SelectHotbarSlot(clampedHotbar);
    }

    private static StructureStateSnapshot BuildStructureSnapshot(Structure s)
    {
        var dto = new StructureStateSnapshot
        {
            Id = s.Id,
            Type = s.StructureType.ToString(),
            X = s.Position.X,
            Y = s.Position.Y,
            TileCol = s.TileCol,
            TileRow = s.TileRow,
            Hp = s.CurrentHp
        };

        if (s.StorageInventory is { UsedSlots: > 0 } chestInv)
        {
            dto.StoredItems = new List<InventoryItemSnapshot>(chestInv.Stacks.Count);
            for (int i = 0; i < chestInv.Stacks.Count; i++)
            {
                ItemStack stack = chestInv.Stacks[i];
                dto.StoredItems.Add(new InventoryItemSnapshot
                {
                    ItemId = stack.Item.Id,
                    Quantity = stack.Quantity
                });
            }
        }

        return dto;
    }

    private static SettlerStateSnapshot BuildSettlerSnapshot(Settler settler)
    {
        var dto = new SettlerStateSnapshot
        {
            Id = settler.Id,
            Name = settler.Name,
            ProfessionId = settler.ProfessionId,
            X = settler.Position.X,
            Y = settler.Position.Y,
            Hp = settler.CurrentHp,
            Happiness = settler.Happiness,
            HasHome = settler.HomeTile.HasValue,
            HomeTileCol = settler.HomeTile?.Col ?? 0,
            HomeTileRow = settler.HomeTile?.Row ?? 0,
            WorkTimer = settler.WorkTimer
        };

        if (settler.Stockpile.UsedSlots > 0)
        {
            dto.Stockpile = new List<InventoryItemSnapshot>(settler.Stockpile.Stacks.Count);
            for (int i = 0; i < settler.Stockpile.Stacks.Count; i++)
            {
                ItemStack stack = settler.Stockpile.Stacks[i];
                dto.Stockpile.Add(new InventoryItemSnapshot
                {
                    ItemId = stack.Item.Id,
                    Quantity = stack.Quantity
                });
            }
        }

        return dto;
    }

    private void RestoreEntitiesFromSnapshot(GameStateDTO snapshot, IEntityFactory entityFactory)
    {
        for (int i = 0; i < snapshot.Resources.Count; i++)
        {
            EntityStateSnapshot r = snapshot.Resources[i];
            if (Enum.TryParse(r.Type, ignoreCase: true, out ResourceType resourceType))
            {
                ResourceNode node = entityFactory.CreateResourceNode(resourceType, new Vector2D(r.X, r.Y));
                node.RestoreHealth(global::System.Math.Min(r.Hp, node.MaxHp));
                AddResource(node);
            }
        }

        for (int i = 0; i < snapshot.Structures.Count; i++)
        {
            StructureStateSnapshot s = snapshot.Structures[i];
            if (Enum.TryParse(s.Type, ignoreCase: true, out StructureType structureType))
            {
                Structure structure = entityFactory.CreateStructure(structureType, s.TileCol, s.TileRow, _level.TileSize);
                structure.RestoreHealth(global::System.Math.Min(s.Hp, structure.MaxHp));
                if (s.StoredItems is not null && structure.StorageInventory is not null)
                {
                    for (int j = 0; j < s.StoredItems.Count; j++)
                    {
                        InventoryItemSnapshot itemDto = s.StoredItems[j];
                        structure.StorageInventory.TryAddItem(_itemResolver(itemDto.ItemId), itemDto.Quantity);
                    }
                }

                AddStructure(structure);
            }
        }

        for (int i = 0; i < snapshot.Enemies.Count; i++)
        {
            EnemyStateSnapshot e = snapshot.Enemies[i];
            if (e.IsBoss)
            {
                Boss boss = entityFactory.CreateBoss(e.Type, new Vector2D(e.X, e.Y), _difficulty);
                boss.RestoreHealth(global::System.Math.Min(e.Hp, boss.MaxHp));
                SpawnBoss(boss);
            }
            else
            {
                Enemy enemy = entityFactory.CreateEnemy(e.Type, new Vector2D(e.X, e.Y), _difficulty);
                enemy.RestoreHealth(global::System.Math.Min(e.Hp, enemy.MaxHp));
                AddEnemy(enemy);
            }
        }

        if (snapshot.Animals is not null)
        {
            for (int i = 0; i < snapshot.Animals.Count; i++)
            {
                AnimalStateSnapshot a = snapshot.Animals[i];
                if (Enum.TryParse(a.Type, ignoreCase: true, out AnimalType animalType))
                {
                    Animal animal = entityFactory.CreateAnimal(animalType, new Vector2D(a.X, a.Y));
                    animal.RestoreHealth(global::System.Math.Min(a.Hp, animal.MaxHp));
                    AddAnimal(animal);
                }
            }
        }

        if (snapshot.FarmPlots is not null)
        {
            for (int i = 0; i < snapshot.FarmPlots.Count; i++)
            {
                FarmPlotSnapshot fp = snapshot.FarmPlots[i];
                _farmingSystem.RestorePlot(fp.TileCol, fp.TileRow, fp.CropId, fp.GrowthSeconds);
            }
        }

        if (snapshot.Settlers is not null)
        {
            for (int i = 0; i < snapshot.Settlers.Count; i++)
            {
                SettlerStateSnapshot st = snapshot.Settlers[i];
                GridPoint? homeTile = st.HasHome ? new GridPoint(st.HomeTileCol, st.HomeTileRow) : null;
                Settler? settler = _settlementManager.CreateSettlerFromSnapshot(
                    st.Id,
                    st.Name,
                    st.ProfessionId,
                    new Vector2D(st.X, st.Y),
                    st.Hp,
                    st.Happiness,
                    homeTile,
                    st.WorkTimer);
                if (settler is null)
                {
                    continue;
                }

                if (st.Stockpile is not null)
                {
                    for (int j = 0; j < st.Stockpile.Count; j++)
                    {
                        InventoryItemSnapshot entry = st.Stockpile[j];
                        settler.Stockpile.TryAddItem(_itemResolver(entry.ItemId), entry.Quantity);
                    }
                }

                AddSettler(settler);
            }

            _settlementManager.RefreshHousing(_level, _structuresView, _structureLookup, _enemiesView);
        }
    }
}
