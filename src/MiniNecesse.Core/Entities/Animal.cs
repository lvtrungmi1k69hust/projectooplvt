using System;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Physics;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a passive wildlife or farm animal (Chicken, Sheep, Cow, Pig)
/// that wanders/grazes peacefully and flees from the player when damaged.
/// </summary>
public sealed class Animal : Character
{
    private const float FLEE_DURATION_SECONDS = 3.5f;
    private const float FLEE_SPEED_MULTIPLIER = 1.45f;
    private const float WANDER_SPEED_MULTIPLIER = 0.55f;

    private readonly AnimalType _animalType;
    private readonly string _displayName;
    private readonly string _primaryDropItemId;
    private readonly int _primaryDropQuantity;
    private readonly string? _secondaryDropItemId;
    private readonly int _secondaryDropQuantity;
    private readonly int _scoreReward;

    private uint _rngState;
    private float _fleeTimer;
    private float _stateTimer;
    private bool _isWandering;
    private Vector2D _wanderDirection;

    /// <summary>Gets the animal species archetype.</summary>
    public AnimalType AnimalType => _animalType;

    /// <summary>Gets the human-readable species name.</summary>
    public string DisplayName => _displayName;

    /// <summary>Gets the primary item identifier dropped on defeat.</summary>
    public string PrimaryDropItemId => _primaryDropItemId;

    /// <summary>Gets the primary drop quantity.</summary>
    public int PrimaryDropQuantity => _primaryDropQuantity;

    /// <summary>Gets the optional secondary item identifier dropped on defeat.</summary>
    public string? SecondaryDropItemId => _secondaryDropItemId;

    /// <summary>Gets the secondary drop quantity.</summary>
    public int SecondaryDropQuantity => _secondaryDropQuantity;

    /// <summary>Gets the score reward awarded when this animal is hunted.</summary>
    public int ScoreReward => _scoreReward;

    /// <summary>Gets a value indicating whether the animal is currently fleeing from danger.</summary>
    public bool IsFleeing => _fleeTimer > 0f;

    /// <summary>Initializes a new instance of the <see cref="Animal"/> class.</summary>
    public Animal(
        string id,
        AnimalType animalType,
        string displayName,
        Vector2D spawnPosition,
        int maxHp,
        float moveSpeed,
        string primaryDropItemId,
        int primaryDropQuantity,
        string? secondaryDropItemId = null,
        int secondaryDropQuantity = 0,
        int scoreReward = 10,
        Vector2D? size = null,
        int? seed = null)
        : base(id, spawnPosition, size ?? new Vector2D(22f, 22f), maxHp, moveSpeed)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Animal display name cannot be empty.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(primaryDropItemId))
        {
            throw new ArgumentException("Primary drop item id cannot be empty.", nameof(primaryDropItemId));
        }

        if (primaryDropQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(primaryDropQuantity), "Primary drop quantity must be positive.");
        }

        _animalType = animalType;
        _displayName = displayName;
        _primaryDropItemId = primaryDropItemId;
        _primaryDropQuantity = primaryDropQuantity;
        _secondaryDropItemId = secondaryDropItemId;
        _secondaryDropQuantity = global::System.Math.Max(0, secondaryDropQuantity);
        _scoreReward = global::System.Math.Max(0, scoreReward);
        _rngState = ComputeDeterministicSeed(id, seed);
        _stateTimer = 0.5f + (NextUnitFloat() * 1.5f);
    }

    /// <inheritdoc />
    public override void TakeDamage(int amount)
    {
        base.TakeDamage(amount);
        if (IsAlive && amount > 0)
        {
            _fleeTimer = FLEE_DURATION_SECONDS;
        }
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (_fleeTimer > 0f)
        {
            _fleeTimer = MathF.Max(0f, _fleeTimer - deltaTime);
        }

        if (_stateTimer > 0f)
        {
            _stateTimer = MathF.Max(0f, _stateTimer - deltaTime);
        }
    }

    /// <summary>
    /// Updates passive grazing/wandering or panic fleeing behavior with tile sliding collision.
    /// </summary>
    public void UpdateBehavior(Player player, IslandLevel level, CollisionDetector collisionDetector, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(collisionDetector);

        if (!IsAlive || !IsActive || deltaTime <= 0f)
        {
            return;
        }

        if (_fleeTimer > 0f)
        {
            Vector2D away = (Center - player.Center).Normalized();
            if (away == Vector2D.Zero)
            {
                away = PickRandomUnitVector();
            }

            Vector2D fleeDelta = away * (MoveSpeed * FLEE_SPEED_MULTIPLIER * deltaTime);
            Move(away, 0f);
            collisionDetector.MoveWithTileCollision(this, fleeDelta, Size, level);
            return;
        }

        if (_stateTimer <= 0f)
        {
            _isWandering = !_isWandering;
            if (_isWandering)
            {
                _wanderDirection = PickRandomUnitVector();
                _stateTimer = 1.2f + (NextUnitFloat() * 1.4f);
            }
            else
            {
                _wanderDirection = Vector2D.Zero;
                _stateTimer = 1.2f + (NextUnitFloat() * 2.0f);
            }
        }

        if (_isWandering && _wanderDirection != Vector2D.Zero)
        {
            Vector2D previous = Position;
            Move(_wanderDirection, 0f);
            Vector2D step = _wanderDirection * (MoveSpeed * WANDER_SPEED_MULTIPLIER * deltaTime);
            Vector2D moved = collisionDetector.MoveWithTileCollision(this, step, Size, level);
            if ((moved - previous).LengthSquared < 0.0001f)
            {
                _stateTimer = 0f;
            }
        }
        else
        {
            Move(Vector2D.Zero, 0f);
        }
    }

    private Vector2D PickRandomUnitVector()
    {
        float angle = NextUnitFloat() * MathF.PI * 2f;
        return new Vector2D(MathF.Cos(angle), MathF.Sin(angle));
    }

    private float NextUnitFloat()
    {
        unchecked
        {
            uint x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x == 0u ? 0x9E3779B9u : x;
            return (_rngState & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static uint ComputeDeterministicSeed(string id, int? explicitSeed)
    {
        unchecked
        {
            uint hash = explicitSeed.HasValue ? (uint)explicitSeed.Value * 0x9E3779B9u : 2166136261u;
            for (int i = 0; i < id.Length; i++)
            {
                hash ^= id[i];
                hash *= 16777619u;
            }

            return hash == 0u ? 0x85EBCA6Bu : hash;
        }
    }
}
