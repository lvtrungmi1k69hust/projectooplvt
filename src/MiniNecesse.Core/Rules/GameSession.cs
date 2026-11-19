using System;
using System.Collections.Generic;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Rules;

/// <summary>
/// Coordinates an active island survival run: world entities, day/night cycle,
/// crafting, rule evaluation, HUD observer notifications, and state snapshots.
/// </summary>
public sealed class GameSession : IDisposable
{
    private const float DEFAULT_WORKBENCH_RADIUS = 80f;
    private const float DEFAULT_PICKUP_RADIUS = 28f;

    private readonly IslandLevel _level;
    private readonly Player _player;
    private readonly CraftingSystem _craftingSystem;
    private readonly DayNightCycle _dayNightCycle;
    private readonly RuleEngine _ruleEngine;
    private readonly Func<string, Item> _itemResolver;
    private readonly Difficulty _difficulty;

    private readonly List<Enemy> _enemies = new();
    private readonly List<ResourceNode> _resources = new();
    private readonly List<Structure> _structures = new();
    private readonly List<Projectile> _projectiles = new();
    private readonly List<DroppedItem> _droppedItems = new();
    private readonly List<IHudObserver> _hudObservers = new();

    private Boss? _activeBoss;
    private GameState _currentState;
    private int _currentWaveIndex;
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

    /// <summary>Gets the configured difficulty preset.</summary>
    public Difficulty Difficulty => _difficulty;

    /// <summary>Gets the current high-level game state.</summary>
    public GameState CurrentState => _currentState;

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

    /// <summary>Raised whenever the session's <see cref="GameState"/> transitions.</summary>
    public event Action<GameState>? OnGameStateChanged;

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
        _currentState = GameState.MainMenu;

        SubscribeInternalEvents();
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

    /// <summary>Transitions the session into active gameplay.</summary>
    public void Start() => SetGameState(GameState.Playing);

    /// <summary>Pauses active gameplay.</summary>
    public void Pause()
    {
        if (_currentState == GameState.Playing)
        {
            SetGameState(GameState.Paused);
        }
    }

    /// <summary>Resumes paused gameplay.</summary>
    public void Resume()
    {
        if (_currentState == GameState.Paused)
        {
            SetGameState(GameState.Playing);
        }
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
        AddEnemy(boss);
        NotifyBossHealth(boss.BossName, boss.CurrentHp, boss.MaxHp, isVisible: true);
    }

    /// <summary>Adds a live projectile into the simulation.</summary>
    public void AddProjectile(Projectile projectile)
    {
        ArgumentNullException.ThrowIfNull(projectile);
        _projectiles.Add(projectile);
    }

    /// <summary>Adds a loose collectible item to the ground.</summary>
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
        return true;
    }

    /// <summary>Advances the simulation by one fixed time step when in <see cref="GameState.Playing"/>.</summary>
    public void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (_currentState != GameState.Playing)
        {
            return;
        }

        _dayNightCycle.Advance(deltaTime);
        NotifyDayPhase();
        _player.Update(deltaTime);
        UpdateEnemies(deltaTime);
        UpdateProjectiles(deltaTime);
        UpdateDroppedItems(deltaTime);
        CleanupInactiveEntities();
        EvaluateSessionOutcome();
    }

    /// <summary>Creates a complete serializable <see cref="GameStateSnapshot"/> for save storage.</summary>
    public GameStateSnapshot CreateSnapshot()
    {
        var snapshot = new GameStateSnapshot
        {
            IslandId = _level.IslandId,
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

    /// <summary>Cleanly unsubscribes internal event handlers to prevent memory leaks.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _player.OnHealthChanged -= HandlePlayerHealthChanged;
        _player.Inventory.OnInventoryChanged -= HandleInventoryChanged;
        _ruleEngine.OnScoreChanged -= HandleScoreChanged;
        _dayNightCycle.OnDayPhaseChanged -= HandleDayPhaseChanged;
        _hudObservers.Clear();
        _disposed = true;
    }

    private void SubscribeInternalEvents()
    {
        _player.OnHealthChanged += HandlePlayerHealthChanged;
        _player.Inventory.OnInventoryChanged += HandleInventoryChanged;
        _ruleEngine.OnScoreChanged += HandleScoreChanged;
        _dayNightCycle.OnDayPhaseChanged += HandleDayPhaseChanged;
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
            if (_projectiles[i].IsActive)
            {
                _projectiles[i].Update(deltaTime);
            }
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
            if (drop.IsActive && Vector2D.DistanceSquared(_player.Center, drop.Center) <= pickupRadiusSq)
            {
                _ruleEngine.ResolvePickup(drop, _player.Inventory, _itemResolver);
            }
        }
    }

    private void CleanupInactiveEntities()
    {
        _projectiles.RemoveAll(p => !p.IsActive);
        _droppedItems.RemoveAll(d => !d.IsActive);
        _resources.RemoveAll(r => !r.IsActive);
        _structures.RemoveAll(s => !s.IsActive);
        _enemies.RemoveAll(e => !e.IsActive);
    }

    private void EvaluateSessionOutcome()
    {
        GameState outcome = _ruleEngine.EvaluateOutcome(_player, _bossDefeated);
        if (outcome != _currentState)
        {
            SetGameState(outcome);
        }
    }

    private void SetGameState(GameState newState)
    {
        if (_currentState == newState)
        {
            return;
        }

        _currentState = newState;
        OnGameStateChanged?.Invoke(_currentState);

        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnGameStateUpdated(_currentState);
        }
    }

    private void HandlePlayerHealthChanged(int currentHp, int maxHp)
    {
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnPlayerHealthUpdated(currentHp, maxHp);
        }
    }

    private void HandleInventoryChanged()
    {
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnInventoryUpdated();
        }
    }

    private void HandleScoreChanged(int newScore)
    {
        for (int i = 0; i < _hudObservers.Count; i++)
        {
            _hudObservers[i].OnScoreUpdated(newScore);
        }
    }

    private void HandleDayPhaseChanged(int dayNumber, DayPhase phase) => NotifyDayPhase();

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
        if (matched.IsBoss)
        {
            _bossDefeated = true;
            if (_activeBoss is not null)
            {
                _activeBoss.OnHealthChanged -= HandleBossHealthChanged;
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

    private void HandleStructureDestroyed(IDamageable destroyed)
    {
        destroyed.OnDefeated -= HandleStructureDestroyed;
        for (int i = 0; i < _structures.Count; i++)
        {
            if (ReferenceEquals(_structures[i], destroyed))
            {
                _level.SetStructureOccupancy(_structures[i].TileCol, _structures[i].TileRow, false);
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
        observer.OnGameStateUpdated(_currentState);
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

    private void PopulateEntitySnapshots(GameStateSnapshot snapshot)
    {
        for (int i = 0; i < _resources.Count; i++)
        {
            ResourceNode r = _resources[i];
            snapshot.Resources.Add(new EntityStateSnapshot { Id = r.Id, Type = r.ResourceType.ToString(), X = r.Position.X, Y = r.Position.Y, Hp = r.CurrentHp });
        }

        for (int i = 0; i < _structures.Count; i++)
        {
            Structure s = _structures[i];
            snapshot.Structures.Add(new EntityStateSnapshot { Id = s.Id, Type = s.StructureType.ToString(), X = s.Position.X, Y = s.Position.Y, Hp = s.CurrentHp });
        }

        for (int i = 0; i < _enemies.Count; i++)
        {
            Enemy e = _enemies[i];
            snapshot.Enemies.Add(new EntityStateSnapshot { Id = e.Id, Type = e.Archetype, X = e.Position.X, Y = e.Position.Y, Hp = e.CurrentHp });
        }
    }
}
