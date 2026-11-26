using System;
using System.Collections.Generic;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.Settlement;

/// <summary>
/// Coordinates settler recruitment, enclosed housing assignments, happiness tracking,
/// trading, stockpile collection, and chunk-culled NPC simulation (SRP).
/// </summary>
public sealed class SettlementManager
{
    private const float HOUSING_RECHECK_INTERVAL = 3.0f;

    private readonly SettlerCatalog _catalog;
    private readonly HousingValidator _housingValidator = new();
    private readonly List<Settler> _settlers = new();
    private readonly IReadOnlyList<Settler> _settlersView;
    private readonly List<HouseValidationResult> _validHouses = new();
    private readonly IReadOnlyList<HouseValidationResult> _validHousesView;
    private readonly HashSet<GridPoint> _occupiedHomeTiles = new();

    private readonly Func<string, Item> _itemResolver;
    private readonly PathfindingCoordinator _pathCoordinator;
    private readonly CollisionDetector _collisionDetector;
    private Action<Vector2D, Vector2D, int, string>? _projectileSpawner;

    private int _settlerSequence;
    private int _lastEvaluatedStructureVersion = -1;
    private float _housingTimer;

    /// <summary>Gets the active JSON settler catalog.</summary>
    public SettlerCatalog Catalog => _catalog;

    /// <summary>Gets cached active settlers managed in loaded chunks.</summary>
    public IReadOnlyList<Settler> Settlers => _settlersView;

    /// <summary>Gets cached detected enclosed valid houses on the active island.</summary>
    public IReadOnlyList<HouseValidationResult> ValidHouses => _validHousesView;

    /// <summary>Initializes a new instance of the <see cref="SettlementManager"/> class.</summary>
    public SettlementManager(
        Func<string, Item> itemResolver,
        PathfindingCoordinator pathCoordinator,
        CollisionDetector collisionDetector,
        SettlerCatalog? catalog = null,
        Action<Vector2D, Vector2D, int, string>? projectileSpawner = null)
    {
        ArgumentNullException.ThrowIfNull(itemResolver);
        ArgumentNullException.ThrowIfNull(pathCoordinator);
        ArgumentNullException.ThrowIfNull(collisionDetector);

        _itemResolver = itemResolver;
        _pathCoordinator = pathCoordinator;
        _collisionDetector = collisionDetector;
        _catalog = catalog ?? SettlerCatalog.CreateDefault();
        _projectileSpawner = projectileSpawner;
        _settlersView = _settlers.AsReadOnly();
        _validHousesView = _validHouses.AsReadOnly();
    }

    /// <summary>Assigns a pooled projectile spawn callback for ranged Hunter settlers.</summary>
    public void ConfigureProjectileSpawner(Action<Vector2D, Vector2D, int, string>? projectileSpawner) =>
        _projectileSpawner = projectileSpawner;

    /// <summary>Re-scans door/wall structures and updates settler housing assignments and happiness.</summary>
    public void RefreshHousing(
        IslandLevel level,
        IReadOnlyList<Structure> structures,
        Func<GridPoint, Structure?> structureLookup,
        IReadOnlyList<Enemy>? enemies = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(structures);
        ArgumentNullException.ThrowIfNull(structureLookup);

        _housingValidator.FindAllValidHouses(level, structures, structureLookup, _validHouses);
        _lastEvaluatedStructureVersion = level.StructureVersion;
        _housingTimer = 0f;

        _occupiedHomeTiles.Clear();

        // Keep valid existing assignments first.
        for (int i = 0; i < _settlers.Count; i++)
        {
            Settler settler = _settlers[i];
            if (!settler.IsAlive || !settler.HomeTile.HasValue)
            {
                continue;
            }

            HouseValidationResult? matchedHouse = FindHouseByAnchor(settler.HomeTile.Value);
            if (matchedHouse is not null && _occupiedHomeTiles.Add(matchedHouse.AnchorTile))
            {
                bool safe = !HasHostileNearTile(level, matchedHouse.AnchorTile, enemies, 192f);
                settler.AssignHome(matchedHouse.AnchorTile, matchedHouse.TorchCount > 0, matchedHouse.InteriorTileCount, safe);
            }
            else
            {
                settler.AssignHome(null);
            }
        }

        // Assign unclaimed valid houses to homeless settlers.
        for (int i = 0; i < _settlers.Count; i++)
        {
            Settler settler = _settlers[i];
            if (!settler.IsAlive || settler.HomeTile.HasValue)
            {
                continue;
            }

            HouseValidationResult? freeHouse = FindFirstUnclaimedHouse();
            if (freeHouse is not null && _occupiedHomeTiles.Add(freeHouse.AnchorTile))
            {
                bool safe = !HasHostileNearTile(level, freeHouse.AnchorTile, enemies, 192f);
                settler.AssignHome(freeHouse.AnchorTile, freeHouse.TorchCount > 0, freeHouse.InteriorTileCount, safe);
            }
            else
            {
                settler.AssignHome(null);
            }
        }
    }

    /// <summary>Recruits a settler from JSON catalog cost, placing it near <paramref name="spawnWorldPos"/>.</summary>
    public bool TryRecruit(
        string professionId,
        Player player,
        Vector2D spawnWorldPos,
        IslandLevel level,
        IReadOnlyList<Structure> structures,
        Func<GridPoint, Structure?> structureLookup,
        out Settler? recruited,
        string? customName = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(structures);
        ArgumentNullException.ThrowIfNull(structureLookup);

        recruited = null;
        if (!_catalog.TryGetProfession(professionId, out SettlerProfessionDefinition? def) || def is null)
        {
            return false;
        }

        if (!player.Inventory.TryRemoveItem(def.RecruitCostItemId, def.RecruitCostQuantity))
        {
            return false;
        }

        player.SyncEquippedFromSelectedSlot();

        _settlerSequence++;
        string name = string.IsNullOrWhiteSpace(customName) ? _catalog.PickName(_settlerSequence - 1) : customName.Trim();
        Settler settler = CreateSettlerInstance($"settler_{_settlerSequence}", name, def, spawnWorldPos);

        AddActiveSettler(settler);
        RefreshHousing(level, structures, structureLookup);
        recruited = settler;
        return true;
    }

    /// <summary>Creates and restores a settler directly from persisted save snapshot data.</summary>
    public Settler? CreateSettlerFromSnapshot(
        string id,
        string name,
        string professionId,
        Vector2D position,
        int hp,
        int happiness,
        GridPoint? homeTile,
        float workTimer)
    {
        if (!_catalog.TryGetProfession(professionId, out SettlerProfessionDefinition? def) || def is null)
        {
            return null;
        }

        Settler settler = CreateSettlerInstance(id, string.IsNullOrWhiteSpace(name) ? def.DisplayName : name, def, position);
        settler.RestoreSettlementState(hp, happiness, homeTile, workTimer);
        return settler;
    }

    /// <summary>Adds a living settler instance directly to the active list.</summary>
    public void AddActiveSettler(Settler settler)
    {
        ArgumentNullException.ThrowIfNull(settler);
        if (settler.IsAlive)
        {
            _settlers.Add(settler);
        }
    }

    /// <summary>Removes an inactive or hibernated settler from the active list.</summary>
    public void RemoveActiveSettlerAt(int index) => _settlers.RemoveAt(index);

    /// <summary>Executes an atomic barter trade between <paramref name="player"/> and <paramref name="settler"/>.</summary>
    public bool TryTrade(Settler settler, string offerId, Player player)
    {
        ArgumentNullException.ThrowIfNull(settler);
        ArgumentNullException.ThrowIfNull(player);
        if (!settler.IsAlive || !player.IsAlive || string.IsNullOrWhiteSpace(offerId))
        {
            return false;
        }

        SettlerTradeOffer? offer = null;
        IReadOnlyList<SettlerTradeOffer> offers = settler.Definition.Trades;
        for (int i = 0; i < offers.Count; i++)
        {
            if (string.Equals(offers[i].OfferId, offerId, StringComparison.OrdinalIgnoreCase))
            {
                offer = offers[i];
                break;
            }
        }

        if (offer is null)
        {
            return false;
        }

        int effectiveCost = settler.GetEffectiveTradeCost(offer.CostQuantity);
        if (!player.Inventory.HasItem(offer.CostItemId, effectiveCost))
        {
            return false;
        }

        Item rewardItem = _itemResolver(offer.RewardItemId);
        if (!player.Inventory.CanAddItem(rewardItem, offer.RewardQuantity))
        {
            return false;
        }

        if (!player.Inventory.TryRemoveItem(offer.CostItemId, effectiveCost))
        {
            return false;
        }

        player.Inventory.TryAddItem(rewardItem, offer.RewardQuantity);
        player.SyncEquippedFromSelectedSlot();
        return true;
    }

    /// <summary>Transfers produced items from a settler's stockpile into the player's inventory.</summary>
    public int TryCollectProducedItems(Settler settler, Player player)
    {
        ArgumentNullException.ThrowIfNull(settler);
        ArgumentNullException.ThrowIfNull(player);

        int movedUnits = 0;
        for (int i = settler.Stockpile.Stacks.Count - 1; i >= 0; i--)
        {
            ItemStack stack = settler.Stockpile.Stacks[i];
            if (player.Inventory.CanAddItem(stack.Item, stack.Quantity) &&
                player.Inventory.TryAddItem(stack.Item, stack.Quantity))
            {
                movedUnits += stack.Quantity;
                settler.Stockpile.TryRemoveItem(stack.Item.Id, stack.Quantity);
            }
        }

        return movedUnits;
    }

    /// <summary>Finds the nearest living settler within <paramref name="maxDistance"/> world units.</summary>
    public Settler? FindNearestSettler(Vector2D worldPosition, float maxDistance = 96f)
    {
        Settler? best = null;
        float bestDistSq = maxDistance * maxDistance;
        for (int i = 0; i < _settlers.Count; i++)
        {
            Settler s = _settlers[i];
            if (!s.IsAlive)
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

    /// <summary>Updates active settlers in loaded chunks with throttled housing re-evaluation (0 GC steady state).</summary>
    public void Update(
        float deltaTime,
        Player player,
        IslandLevel level,
        IReadOnlyList<Enemy> enemies,
        IReadOnlyList<ResourceNode> resources,
        IReadOnlyList<Structure> structures,
        Func<GridPoint, Structure?> structureLookup,
        DayPhase phase)
    {
        _housingTimer += deltaTime;
        if (level.StructureVersion != _lastEvaluatedStructureVersion || _housingTimer >= HOUSING_RECHECK_INTERVAL)
        {
            RefreshHousing(level, structures, structureLookup, enemies);
        }

        for (int i = _settlers.Count - 1; i >= 0; i--)
        {
            Settler s = _settlers[i];
            if (!s.IsAlive || !s.IsActive)
            {
                _settlers.RemoveAt(i);
                continue;
            }

            if (level.UsesLazyChunkLoading && !level.IsChunkLoaded(level.GetChunkCoordForWorld(s.Center)))
            {
                continue;
            }

            s.Update(deltaTime);
            s.CurrentStrategy?.SetWorldContext(enemies, resources, phase, _projectileSpawner);
            s.ExecuteAI(player, level, deltaTime);
        }
    }

    /// <summary>Snapshots previous positions of loaded active settlers before a fixed step.</summary>
    public void CapturePreviousPositions()
    {
        for (int i = 0; i < _settlers.Count; i++)
        {
            if (_settlers[i].IsActive)
            {
                _settlers[i].CapturePreviousPosition();
            }
        }
    }

    /// <summary>Clears all active settlers and cached housing detections.</summary>
    public void Clear()
    {
        _settlers.Clear();
        _validHouses.Clear();
        _occupiedHomeTiles.Clear();
        _lastEvaluatedStructureVersion = -1;
        _housingTimer = 0f;
    }

    private Settler CreateSettlerInstance(string id, string name, SettlerProfessionDefinition def, Vector2D spawnPos)
    {
        float stagger = (_settlerSequence % 7) * 0.08f;
        ISettlerStrategy strategy = def.Profession == SettlerProfession.Hunter
            ? new HunterSettlerStrategy(_itemResolver, _pathCoordinator, _collisionDetector, stagger)
            : new MinerSettlerStrategy(_itemResolver, _pathCoordinator, _collisionDetector, stagger);

        return new Settler(id, name, def, spawnPos, strategy);
    }

    private HouseValidationResult? FindHouseByAnchor(GridPoint anchor)
    {
        for (int i = 0; i < _validHouses.Count; i++)
        {
            if (_validHouses[i].AnchorTile == anchor)
            {
                return _validHouses[i];
            }
        }

        return null;
    }

    private HouseValidationResult? FindFirstUnclaimedHouse()
    {
        for (int i = 0; i < _validHouses.Count; i++)
        {
            if (!_occupiedHomeTiles.Contains(_validHouses[i].AnchorTile))
            {
                return _validHouses[i];
            }
        }

        return null;
    }

    private static bool HasHostileNearTile(IslandLevel level, GridPoint tile, IReadOnlyList<Enemy>? enemies, float radius)
    {
        if (enemies is null || enemies.Count == 0)
        {
            return false;
        }

        Vector2D tileWorld = level.TileToWorldCenter(tile.Col, tile.Row);
        float radiusSq = radius * radius;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].IsAlive && Vector2D.DistanceSquared(tileWorld, enemies[i].Center) <= radiusSq)
            {
                return true;
            }
        }

        return false;
    }
}
