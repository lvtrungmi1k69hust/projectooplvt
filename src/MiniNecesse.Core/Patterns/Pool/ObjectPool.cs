using System;
using System.Collections.Generic;

namespace MiniNecesse.Core.Patterns.Pool;

/// <summary>
/// Generic Object Pool reusing high-frequency instances (such as <see cref="MiniNecesse.Core.Entities.Projectile"/>
/// and <see cref="MiniNecesse.Core.Entities.DroppedItem"/>) to prevent GC spikes during fixed-timestep updates.
/// <para>
/// Complexity: Acquire O(1), Release O(1).
/// </para>
/// </summary>
/// <typeparam name="T">Reference type managed by this pool.</typeparam>
public sealed class ObjectPool<T> where T : class
{
    private readonly Func<T> _factory;
    private readonly Action<T>? _onRelease;
    private readonly Stack<T> _available;
    private readonly HashSet<T> _inPoolLookup;
    private readonly int _maxCapacity;
    private int _totalCreatedCount;
    private int _activeCount;

    /// <summary>Gets the number of idle instances currently ready for reuse.</summary>
    public int AvailableCount => _available.Count;

    /// <summary>Gets the number of currently checked-out active instances.</summary>
    public int ActiveCount => _activeCount;

    /// <summary>Gets total instances allocated over the lifetime of this pool.</summary>
    public int TotalCreatedCount => _totalCreatedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
    /// </summary>
    /// <param name="factory">Delegate creating a new instance when the pool is empty.</param>
    /// <param name="onRelease">Optional cleanup callback invoked when an instance returns to the pool.</param>
    /// <param name="initialCapacity">Initial pre-allocated stack capacity.</param>
    /// <param name="maxCapacity">Maximum number of idle instances retained in the pool.</param>
    public ObjectPool(
        Func<T> factory,
        Action<T>? onRelease = null,
        int initialCapacity = 32,
        int maxCapacity = 256)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity cannot be negative.");
        }

        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Max capacity must be positive.");
        }

        _factory = factory;
        _onRelease = onRelease;
        _maxCapacity = maxCapacity;
        _available = new Stack<T>(initialCapacity);
        _inPoolLookup = new HashSet<T>(ReferenceEqualityComparer.Instance);
    }

    /// <summary>Pre-allocates <paramref name="count"/> idle instances up front before gameplay starts.</summary>
    public void Prewarm(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Prewarm count cannot be negative.");
        }

        int target = global::System.Math.Min(count, _maxCapacity - _available.Count);
        for (int i = 0; i < target; i++)
        {
            T instance = _factory();
            _totalCreatedCount++;
            _available.Push(instance);
            _inPoolLookup.Add(instance);
        }
    }

    /// <summary>
    /// Acquires an idle instance from the pool in O(1), or allocates one via factory if empty.
    /// </summary>
    public T Acquire()
    {
        T instance;
        if (_available.Count > 0)
        {
            instance = _available.Pop();
            _inPoolLookup.Remove(instance);
        }
        else
        {
            instance = _factory();
            _totalCreatedCount++;
        }

        _activeCount++;
        return instance;
    }

    /// <summary>
    /// Returns <paramref name="instance"/> back to the pool in O(1), ignoring duplicate releases safely.
    /// </summary>
    public void Release(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (_inPoolLookup.Contains(instance))
        {
            return;
        }

        _onRelease?.Invoke(instance);
        _activeCount = global::System.Math.Max(0, _activeCount - 1);

        if (_available.Count < _maxCapacity)
        {
            _available.Push(instance);
            _inPoolLookup.Add(instance);
        }
    }
}
