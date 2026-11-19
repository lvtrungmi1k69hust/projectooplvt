using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Pool;
using MiniNecesse.Core.Patterns.State;
using MiniNecesse.Core.Physics;

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

    private readonly IslandLevel _level;
    private readonly Player _player;
    private readonly CraftingSystem _craftingSystem;
    private readonly DayNightCycle _dayNightCycle;
    private readonly RuleEngine _ruleEngine;
    private readonly Func<string, Item> _itemResolver;
    private readonly Difficulty _difficulty;

    private readonly GameStateMachine _stateMachine = new();
    private readonly CollisionDetector _collisionDetector = new();
    private readonly SpatialHashGrid<Enemy> _enemySpatialGrid = new(96f);
    private readonly SpatialHashGrid<Player> _playerSpatialGrid = new(96f);
    private readonly List<Enemy> _enemyQueryBuffer = new(32);
    private readonly List<Player> _playerQueryBuffer = new(4);
    private readonly List<Enemy> _waveSpawnBuffer = new(24);

    private readonly ObjectPool<Projectile> _projectilePool;
    private readonly ObjectPool<DroppedItem> _droppedItemPool;

    private readonly List<Enemy> _enemies = new();
    private readonly List<ResourceNode> _resources = new();
    private readonly List<Structure> _structures = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<DroppedItem> _droppedItems = new();
    private readonly List<IHudObserver> _hudObservers = new();

    private RaidWaveSpawner? _waveSpawner;
    private Boss? _activeBoss;
    private int _currentWaveIndex;
    private int _pooledProjectileSequence;
    private int _pooledDropSequence;
    private bool _bossDefeated;
    private bool _disposed;

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

    /// <summary>Gets the pooled projectile cache for zero-allocation combat.</summary>
    public ObjectPool<Projectile> ProjectilePool => _projectilePool;

    /// <summary>Gets the pooled dropped-item cache for zero-allocation harvesting.</summary>
    public ObjectPool<DroppedItem> DroppedItemPool => _droppedItemPool;

    /// <summary>Gets the configured difficulty preset.</summary>
    public Difficulty Difficulty => _difficulty;

    /// <summary>Gets the current high-level game state from the state machine.</summary>
    public GameState CurrentState => _stateMachine.CurrentStateType;

    /// <summary>Gets the active island boss, if spawned.</summary>
    public Boss? ActiveBoss => _activeBoss;

    /// <summary>Gets whether the island boss has been defeated.</summary>
    public bool IsBossDefeated => _bossDefeated;

    /// <summary>Gets the current raid wave index.</summary>
    public int CurrentWaveIndex => _currentWaveIndex;

    /// <summary>Gets a read-only view of all active enemies (including boss).</summary>
    public IReadOnlyList<Enemy> Enemies => _enemies.AsReadOnly();

    /// <summary>Gets a read-only view of all resource nodes.</summary>
    public IReadOnlyList<ResourceNode> Resources => _resources.AsReadOnly();

    /// <summary>Gets a read-only view of all placed structures.</summary>
    public IReadOnlyList<Structure> Structures => _structures.AsReadOnly();

    /// <summary>Gets a read-only view of all active projectiles.</summary>
    public IReadOnlyList<Projectile> Projectiles => _projectiles.AsReadOnly();

    /// <summary>Gets a read-only view of all loose dropped items.</summary>
    public IReadOnlyList<DroppedItem> DroppedItems => _droppedItems.AsReadOnly();

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
        Difficulty difficulty = Difficulty.Normal)
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
        _difficulty = difficulty;

        _projectilePool = new ObjectPool<Projectile>(CreatePooledProjectile, p => p.Deactivate(), initialCapacity: 32, maxCapacity: 256);
        _droppedItemPool = new ObjectPool<DroppedItem>(CreatePooledDrop, d => d.Deactivate(), initialCapacity: 24, maxCapacity: 128);
        _projectilePool.Prewarm(16);
        _droppedItemPool.Prewarm(12);

        ConfigureStateMachine();
        SubscribeInternalEvents();
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
    public void Start() => _stateMachine.TransitionTo(GameState.Playing);

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
        Vector2D delta = normalized * (_player.MoveSpeed * terrainFactor * deltaTime);

        _player.Move(normalized, 0f);
        _collisionDetector.MoveWithTileCollision(_player, delta, _player.Size, _level);
    }

    /// <summary>Adds a harvestable resource node to the active island.</summary>
    public void AddResource(ResourceNode resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resources.Add(resource);
    }

    /// <summary>Places a structure onto the island and updates tile solid occupancy.</summary>
    public void AddStructure(Structure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        _structures.Add(structure);

        if (structure.IsSolid && _level.IsInBounds(structure.TileCol, structure.TileRow))
        {
            _level.SetStructureOccupancy(structure.TileCol, structure.TileRow, true);
            structure.OnDefeated += HandleStructureDestroyed;
        }

        RequestSound("build");
    }

    /// <summary>Looks up a living structure occupying <paramref name="tile"/> (used by wall-breaching AI).</summary>
    public Structure? FindStructureAtTile(GridPoint tile)
    {
        for (int i = 0; i < _structures.Count; i++)
        {
            Structure s = _structures[i];
            if (s.IsAlive && s.TileCol == tile.Col && s.TileRow == tile.Row)
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>Registers a hostile enemy into the active session.</summary>
    public void AddEnemy(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        _enemies.Add(enemy);
        enemy.OnDefeated += HandleEnemyDefeated;
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

    /// <summary>Delegates fixed-timestep simulation to the active <see cref="IGameState"/> in <see cref="StateMachine"/>.</summary>
    public void Update(float deltaTime) => _stateMachine.Update(deltaTime);

    /// <summary>Creates a complete serializable <see cref="GameStateDTO"/> for save storage.</summary>
    public GameStateDTO CreateSnapshot()
    {
        var snapshot = new GameStateDTO
        {
            IslandId = _level.IslandId,
            Difficulty = _difficulty.ToString(),
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
        RestorePlayerFromSnapshot(snapshot.Player);
        _ruleEngine.RestoreScore(snapshot.Score);
        _dayNightCycle.RestoreState(snapshot.DayNumber, snapshot.TimeOfDaySeconds);
        _currentWaveIndex = snapshot.CurrentWaveIndex;
        _waveSpawner?.RestoreWaveIndex(snapshot.CurrentWaveIndex);
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
        _dayNightCycle.Advance(deltaTime);
        NotifyDayPhase();
        _player.Update(deltaTime);
        UpdateEnemies(deltaTime);
        RebuildSpatialGrids();
        UpdateProjectiles(deltaTime);
        UpdateDroppedItems(deltaTime);
        CleanupInactiveEntities();
        EvaluateSessionOutcome();
    }

    private void RebuildSpatialGrids()
    {
        _enemySpatialGrid.Clear();
        _enemySpatialGrid.InsertRange(_enemies);

        _playerSpatialGrid.Clear();
        if (_player.IsAlive)
        {
            _playerSpatialGrid.Insert(_player);
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
            _enemySpatialGrid,
            _enemyQueryBuffer,
            out Enemy? hitEnemy,
            out _);

        if (hit && hitEnemy is not null)
        {
            _ruleEngine.ResolveProjectileHit(projectile, hitEnemy);
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
        RemoveInactiveInPlace(_structures);
        RemoveInactiveInPlace(_enemies);
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
        NotifyDayPhase();
        if (phase == DayPhase.Night && _waveSpawner is not null && !_waveSpawner.HasCompletedAllWaves)
        {
            int spawned = _waveSpawner.SpawnNextWave(_player, _waveSpawnBuffer);
            for (int i = 0; i < spawned; i++)
            {
                AddEnemy(_waveSpawnBuffer[i]);
            }

            _currentWaveIndex = _waveSpawner.NextWaveIndex;
            RequestSound("raid_horn");
        }
    }

    private void NotifyDayPhase()
    {
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnDayPhaseUpdated(
                _dayNightCycle.DayNumber,
                _dayNightCycle.CurrentPhase,
                _dayNightCycle.NormalizedPhaseProgress);
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
                _level.SetStructureOccupancy(_structures[i].TileCol, _structures[i].TileRow, false);
                RequestSound("structure_break");
                return;
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
            SelectedHotbarIndex = _player.SelectedHotbarIndex
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

        for (int i = 0; i < _structures.Count; i++)
        {
            Structure s = _structures[i];
            snapshot.Structures.Add(new StructureStateSnapshot { Id = s.Id, Type = s.StructureType.ToString(), X = s.Position.X, Y = s.Position.Y, TileCol = s.TileCol, TileRow = s.TileRow, Hp = s.CurrentHp });
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy e = _enemies[i];
            snapshot.Enemies.Add(new EnemyStateSnapshot { Id = e.Id, Type = e.Archetype, X = e.Position.X, Y = e.Position.Y, Hp = e.CurrentHp, IsBoss = e.IsBoss });
        }
    }

    private void ClearActiveWorldEntities()
    {
        for (int i = 0; i < _structures.Count; i++)
        {
            Structure s = _structures[i];
            if (s.IsSolid && _level.IsInBounds(s.TileCol, s.TileRow))
            {
                _level.SetStructureOccupancy(s.TileCol, s.TileRow, false);
            }
        }

        _resources.Clear();
        _structures.Clear();
        _enemies.Clear();
        _projectiles.Clear();
        _droppedItems.Clear();
        _activeBoss = null;
        _bossDefeated = false;
    }

    private void RestorePlayerFromSnapshot(PlayerSnapshot playerDto)
    {
        _player.SetPosition(new Vector2D(playerDto.X, playerDto.Y));
        _player.RestoreHealth(playerDto.Hp);
        _player.Inventory.Clear();

        for (int i = 0; i < playerDto.Inventory.Count; i++)
        {
            InventoryItemSnapshot entry = playerDto.Inventory[i];
            Item resolved = _itemResolver(entry.ItemId);
            _player.Inventory.TryAddItem(resolved, entry.Quantity);
        }

        int clampedHotbar = global::System.Math.Clamp(playerDto.SelectedHotbarIndex, 0, _player.HotbarCapacity - 1);
        _player.SelectHotbarSlot(clampedHotbar);
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
    }
}
