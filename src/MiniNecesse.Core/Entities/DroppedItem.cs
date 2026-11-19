using System;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;

namespace MiniNecesse.Core.Entities;

/// <summary>
/// Represents a loose collectible item resting on the island ground that can be picked up into an inventory.
/// Supports in-place reset for object pooling.
/// </summary>
public sealed class DroppedItem : Entity, ICollectible
{
    private const float DEFAULT_DESPAWN_SECONDS = 120f;

    private string _itemId;
    private int _quantity;
    private bool _isCollected;
    private float _remainingLifetime;

    /// <inheritdoc />
    public string ItemId => _itemId;

    /// <inheritdoc />
    public int Quantity => _quantity;

    /// <inheritdoc />
    public bool IsCollected => _isCollected;

    /// <summary>Gets remaining seconds before this dropped item despawns.</summary>
    public float RemainingLifetime => _remainingLifetime;

    /// <inheritdoc />
    public event Action<ICollectible>? OnCollected;

    /// <summary>
    /// Initializes a new instance of the <see cref="DroppedItem"/> class.
    /// </summary>
    public DroppedItem(
        string id,
        string itemId,
        int quantity,
        Vector2D position,
        float despawnSeconds = DEFAULT_DESPAWN_SECONDS)
        : base(id, position, new Vector2D(16f, 16f))
    {
        _itemId = string.Empty;
        Configure(itemId, quantity, position, despawnSeconds);
    }

    /// <summary>Reinitializes this dropped item instance for object pooling.</summary>
    public void Reset(string itemId, int quantity, Vector2D position, float despawnSeconds = DEFAULT_DESPAWN_SECONDS)
    {
        Configure(itemId, quantity, position, despawnSeconds);
        Activate();
    }

    /// <inheritdoc />
    public bool TryCollect(Func<string, int, bool> tryStore)
    {
        ArgumentNullException.ThrowIfNull(tryStore);

        if (_isCollected || !IsActive)
        {
            return false;
        }

        if (!tryStore(_itemId, _quantity))
        {
            return false;
        }

        _isCollected = true;
        Deactivate();
        OnCollected?.Invoke(this);
        return true;
    }

    /// <inheritdoc />
    public override void Update(float deltaTime)
    {
        if (deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (!IsActive || _isCollected)
        {
            return;
        }

        _remainingLifetime -= deltaTime;
        if (_remainingLifetime <= 0f)
        {
            _remainingLifetime = 0f;
            Deactivate();
        }
    }

    private void Configure(string itemId, int quantity, Vector2D position, float despawnSeconds)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Dropped itemId cannot be empty.", nameof(itemId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Dropped quantity must be positive.");
        }

        if (despawnSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(despawnSeconds), "Despawn time must be positive.");
        }

        _itemId = itemId;
        _quantity = quantity;
        _isCollected = false;
        _remainingLifetime = despawnSeconds;
        UpdatePosition(position);
    }
}
