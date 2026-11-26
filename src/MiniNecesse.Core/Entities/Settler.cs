using System;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Settlement;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a friendly recruited NPC settler (Miner or Hunter) with FSM state,
/// replaceable <see cref="ISettlerStrategy"/>, housing happiness, work stockpile, and trade pricing.
/// </summary>
public sealed class Settler : Character
{
    private const int DEFAULT_STOCKPILE_SLOTS = 6;
    private const int HOMELESS_BASE_HAPPINESS = 30;

    private readonly string _name;
    private readonly SettlerProfessionDefinition _definition;
    private readonly Inventory _stockpile = new(DEFAULT_STOCKPILE_SLOTS);

    private SettlerState _state = SettlerState.Idle;
    private ISettlerStrategy? _strategy;
    private GridPoint_Ref? _homeTile;
    private int _happiness = HOMELESS_BASE_HAPPINESS;
    private float _workTimer;
    private int _productionCursor;
    private float _attackCooldownRemaining;

    // Nested light value wrapper avoiding Nullable<GridPoint> confusion while exposing clean Core.AI.GridPoint?
    private readonly struct GridPoint_Ref
    {
        public readonly AI.GridPoint Point;
        public GridPoint_Ref(AI.GridPoint point) => Point = point;
    }

    /// <summary>Gets the display name of this settler.</summary>
    public string Name => _name;

    /// <summary>Gets the occupation enum of this settler.</summary>
    public SettlerProfession Profession => _definition.Profession;

    /// <summary>Gets the occupation identifier string matching <c>settlers.json</c>.</summary>
    public string ProfessionId => _definition.ProfessionId;

    /// <summary>Gets the JSON profession profile governing work interval and trade offers.</summary>
    public SettlerProfessionDefinition Definition => _definition;

    /// <summary>Gets the active FSM state of this settler.</summary>
    public SettlerState State => _state;

    /// <summary>Gets the assigned <see cref="ISettlerStrategy"/>.</summary>
    public ISettlerStrategy? CurrentStrategy => _strategy;

    /// <summary>Gets happiness score in [0, 100] driven by assigned housing quality.</summary>
    public int Happiness => _happiness;

    /// <summary>Gets the assigned valid enclosed house interior tile, if any.</summary>
    public AI.GridPoint? HomeTile => _homeTile?.Point;

    /// <summary>Gets whether this settler currently occupies a valid enclosed house.</summary>
    public bool HasValidHome => _homeTile.HasValue;

    /// <summary>Gets accumulated seconds toward the next resource production cycle.</summary>
    public float WorkTimer => _workTimer;

    /// <summary>Gets the settler's gathered stockpile inventory ready for collection by the player.</summary>
    public Inventory Stockpile => _stockpile;

    /// <summary>Gets whether the settler can perform a defensive combat strike this tick.</summary>
    public bool CanAttack => IsAlive && _attackCooldownRemaining <= 0f;

    /// <summary>Raised when the settler's FSM state changes.</summary>
    public event Action<Settler, SettlerState>? OnStateChanged;

    /// <summary>Initializes a new instance of the <see cref="Settler"/> class.</summary>
    public Settler(
        string id,
        string name,
        SettlerProfessionDefinition definition,
        Vector2D spawnPosition,
        ISettlerStrategy? initialStrategy = null)
        : base(
            id,
            spawnPosition,
            new Vector2D(24f, 24f),
            (definition ?? throw new ArgumentNullException(nameof(definition))).MaxHp,
            definition.MoveSpeed)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Settler name cannot be empty.", nameof(name));
        }

        _name = name;
        _definition = definition;
        _strategy = initialStrategy;
    }

    /// <summary>Assigns or swaps the active settler AI strategy (GoF Strategy).</summary>
    public void SetStrategy(ISettlerStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _strategy = strategy;
    }

    /// <summary>Transitions the settler FSM state.</summary>
    public void SetState(SettlerState newState)
    {
        if (_state == newState)
        {
            return;
        }

        _state = newState;
        OnStateChanged?.Invoke(this, _state);
    }

    /// <summary>Assigns housing status and recomputes happiness in [0, 100].</summary>
    public void AssignHome(AI.GridPoint? homeTile, bool hasTorch = false, int roomTiles = 0, bool areaSafe = true)
    {
        if (!homeTile.HasValue)
        {
            _homeTile = null;
            _happiness = areaSafe ? HOMELESS_BASE_HAPPINESS : HOMELESS_BASE_HAPPINESS - 10;
            return;
        }

        _homeTile = new GridPoint_Ref(homeTile.Value);
        int score = HOMELESS_BASE_HAPPINESS + 40;
        if (hasTorch) score += 15;
        if (roomTiles >= 12) score += 10;
        if (areaSafe) score += 5;
        _happiness = global::System.Math.Clamp(score, 0, 100);
    }

    /// <summary>Computes happiness-adjusted trade item cost (20% discount when >= 80, +20% when &lt; 40).</summary>
    public int GetEffectiveTradeCost(int baseCost)
    {
        if (baseCost <= 0)
        {
            return 0;
        }

        if (_happiness >= 80)
        {
            return global::System.Math.Max(1, (int)MathF.Floor(baseCost * 0.80f));
        }

        if (_happiness < 40)
        {
            return (int)MathF.Ceiling(baseCost * 1.20f);
        }

        return baseCost;
    }

    /// <summary>Starts defensive strike cooldown in seconds.</summary>
    public void TriggerAttackCooldown(float cooldown = 1.1f) =>
        _attackCooldownRemaining = global::System.Math.Max(0.25f, cooldown);

    /// <summary>
    /// Advances production progress based on happiness and appends produced items to <see cref="Stockpile"/>.
    /// Returns <see langword="true"/> when a work cycle completes.
    /// </summary>
    public bool TryAdvanceProduction(float deltaTime, Func<string, Item> itemResolver)
    {
        ArgumentNullException.ThrowIfNull(itemResolver);
        if (!IsAlive || deltaTime <= 0f || _definition.ProducedItems.Count == 0)
        {
            return false;
        }

        // Higher happiness boosts productivity cleanly from ~0.9x up to 1.25x.
        float efficiency = 0.75f + (_happiness / 200f);
        _workTimer += deltaTime * efficiency;
        if (_workTimer < _definition.WorkIntervalSeconds)
        {
            return false;
        }

        _workTimer -= _definition.WorkIntervalSeconds;
        SettlerProducedEntry entry = _definition.ProducedItems[_productionCursor % _definition.ProducedItems.Count];
        _productionCursor++;

        Item item = itemResolver(entry.ItemId);
        return _stockpile.TryAddItem(item, entry.Quantity);
    }

    /// <summary>Executes the settler's active <see cref="ISettlerStrategy"/> for one fixed step.</summary>
    public void ExecuteAI(Player player, IslandLevel level, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(level);
        if (!IsAlive || !IsActive)
        {
            return;
        }

        _strategy?.Execute(this, player, level, deltaTime);
    }

    /// <summary>Restores persisted happiness, housing anchor, and production timer from snapshot data.</summary>
    public void RestoreSettlementState(int hp, int happiness, AI.GridPoint? homeTile, float workTimer)
    {
        RestoreHealth(global::System.Math.Clamp(hp, 0, MaxHp));
        _happiness = global::System.Math.Clamp(happiness, 0, 100);
        _homeTile = homeTile.HasValue ? new GridPoint_Ref(homeTile.Value) : null;
        _workTimer = MathF.Max(0f, workTimer);
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (_attackCooldownRemaining > 0f)
        {
            _attackCooldownRemaining = global::System.Math.Max(0f, _attackCooldownRemaining - deltaTime);
        }
    }
}
