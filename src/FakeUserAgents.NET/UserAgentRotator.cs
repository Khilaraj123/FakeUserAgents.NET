using Dotnet.FakeUserAgents.Internal;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Dotnet.FakeUserAgents;

/// <summary>
/// Provides high-performance, thread-safe rotation through realistic user agent profiles.
/// Employs lock-free atomic hot paths for <see cref="RotationStrategy.RoundRobin"/> and <see cref="RotationStrategy.Random"/>,
/// amortized lock-free execution for <see cref="RotationStrategy.Shuffled"/>, and race-free session affinity.
/// </summary>
public sealed class UserAgentRotator
{
    private readonly ImmutableArray<UserAgentInfo> _pool;
    private readonly Random _random;
    private readonly bool _isSharedRandom;
    private readonly int _maxSessions;

    // Concurrency synchronization primitives
    private readonly object _shuffleSync = new();
    private readonly object _noRepeatSync = new();
    private readonly object _randomSync = new();

    // Round-robin & shuffled state
    private int _currentIndex = -1;
    private readonly int[] _shuffledIndices;
    private readonly double[] _cumulativeWeights;
    private readonly double _totalWeight;
    private int _shuffledPosition;
    private int _lastPickedIndex = -1;

    // Session cache: uses Lazy<UserAgentInfo> to guarantee value factory executes strictly once per key
    private readonly ConcurrentDictionary<string, Lazy<UserAgentInfo>> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the rotation strategy configured for this rotator.
    /// </summary>
    public RotationStrategy Strategy { get; }

    /// <summary>
    /// Gets the number of matching user agents in this rotator's pool.
    /// </summary>
    public int AvailableCount => _pool.Length;

    /// <summary>
    /// Gets the count of currently active persistent sessions.
    /// </summary>
    public int ActiveSessionCount => _sessions.Count;

    /// <summary>
    /// Returns an immutable read-only view of the candidate user agent profiles in this rotator's pool.
    /// </summary>
    public IReadOnlyList<UserAgentInfo> GetAvailableProfiles() => _pool;

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> using <see cref="RotationStrategy.Shuffled"/>.
    /// </summary>
    public UserAgentRotator()
        : this(strategy: RotationStrategy.Shuffled, seed: null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> using the specified strategy.
    /// </summary>
    /// <param name="strategy">The rotation strategy to employ.</param>
    public UserAgentRotator(RotationStrategy strategy)
        : this(strategy: strategy, seed: null) { }

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> with a deterministic seed for testing.
    /// </summary>
    /// <param name="strategy">The rotation strategy to employ.</param>
    /// <param name="seed">A seed for reproducible pseudo-random generation.</param>
    public UserAgentRotator(RotationStrategy strategy, int seed)
        : this(pool: UaDatabase.Entries, strategy: strategy, random: new Random(seed)) { }

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> with a custom random generator.
    /// </summary>
    /// <param name="strategy">The rotation strategy to employ.</param>
    /// <param name="random">Optional custom random generator.</param>
    public UserAgentRotator(RotationStrategy strategy, Random? random)
        : this(pool: UaDatabase.Entries, strategy: strategy, random: random) { }

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> with optional filters and strategy.
    /// </summary>
    /// <param name="strategy">The rotation strategy to employ (default: <see cref="RotationStrategy.Shuffled"/>).</param>
    /// <param name="browser">Optional browser filter.</param>
    /// <param name="device">Optional device category filter.</param>
    /// <param name="os">Optional operating system filter.</param>
    /// <param name="seed">Optional random seed for deterministic generation.</param>
    /// <param name="maxSessions">Maximum number of concurrent session mappings before eviction (default: 10,000).</param>
    public UserAgentRotator(
        RotationStrategy strategy = RotationStrategy.Shuffled,
        Browser? browser = null,
        DeviceType? device = null,
        OperatingSystemType? os = null,
        int? seed = null,
        int maxSessions = 10_000)
        : this(
            pool: ResolvePool(browser, device, os),
            strategy: strategy,
            random: seed.HasValue ? new Random(seed.Value) : null,
            maxSessions: maxSessions)
    { }

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> with a custom filter predicate.
    /// </summary>
    /// <param name="filter">A predicate used to select which user agents to include.</param>
    /// <param name="strategy">The rotation strategy.</param>
    /// <param name="random">Optional custom random generator.</param>
    /// <param name="maxSessions">Maximum session cache capacity.</param>
    public UserAgentRotator(
        Func<UserAgentInfo, bool>? filter,
        RotationStrategy strategy = RotationStrategy.Shuffled,
        Random? random = null,
        int maxSessions = 10_000)
        : this(
            pool: FilterEntries(UaDatabase.Entries, filter),
            strategy: strategy,
            random: random,
            maxSessions: maxSessions)
    { }

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentRotator"/> from an explicit collection of user agents.
    /// </summary>
    /// <param name="pool">The collection of user agents to rotate across.</param>
    /// <param name="strategy">The rotation strategy.</param>
    /// <param name="random">Optional custom random generator.</param>
    /// <param name="maxSessions">Maximum session cache capacity.</param>
    public UserAgentRotator(
        IEnumerable<UserAgentInfo> pool,
        RotationStrategy strategy = RotationStrategy.Shuffled,
        Random? random = null,
        int maxSessions = 10_000)
    {
        var array = pool is ImmutableArray<UserAgentInfo> immutable ? immutable : pool.ToImmutableArray();
        if (array.Length == 0)
        {
            array = ImmutableArray.Create(UaDatabase.DefaultEntry);
        }

        _pool = array;
        Strategy = strategy;
        _isSharedRandom = random is null;
        _random = random ?? Random.Shared;
        _maxSessions = Math.Max(100, maxSessions);

        _shuffledIndices = new int[_pool.Length];
        _cumulativeWeights = new double[_pool.Length];
        double sum = 0;
        for (int i = 0; i < _shuffledIndices.Length; i++)
        {
            _shuffledIndices[i] = i;
            sum += _pool[i].Weight;
            _cumulativeWeights[i] = sum;
        }
        _totalWeight = sum;

        ShuffleIndicesInternal();
    }

    /// <summary>
    /// Returns the next rotated <see cref="UserAgentInfo"/> according to the configured strategy.
    /// High-performance and thread-safe.
    /// </summary>
    /// <returns>A rotated <see cref="UserAgentInfo"/> entry.</returns>
    public UserAgentInfo Next()
    {
        int index = Strategy switch
        {
            RotationStrategy.RoundRobin => PickRoundRobin(),
            RotationStrategy.Random => PickRandom(),
            RotationStrategy.Shuffled => PickShuffled(),
            RotationStrategy.NoImmediateRepeat => PickNoImmediateRepeat(),
            _ => PickRandom()
        };

        return _pool[index];
    }

    /// <summary>
    /// Returns the next rotated raw User-Agent header string with zero heap allocation.
    /// </summary>
    /// <returns>The raw User-Agent string.</returns>
    public string NextUserAgent() => Next().UserAgent;

    /// <summary>
    /// Returns the next rotated user agent formatted as a metadata dictionary.
    /// Note: Allocates a new dictionary on each call. For high-throughput scrapers, use <see cref="Next"/> or <see cref="NextUserAgent"/>.
    /// </summary>
    /// <returns>A dictionary containing the user agent metadata.</returns>
    public Dictionary<string, string> NextAsDictionary() => Next().ToDictionary();

    // ---- Session Persistence --------------------------------------------------

    /// <summary>
    /// Returns a user agent persistently pinned to the specified <paramref name="sessionId"/>.
    /// Guarantees race-free, atomic creation so the generator is called at most once per session key.
    /// Subsequent calls with the same <paramref name="sessionId"/> return the identical user agent.
    /// </summary>
    /// <param name="sessionId">The unique identifier for the session, cookie, or client.</param>
    /// <returns>The <see cref="UserAgentInfo"/> associated with the session.</returns>
    public UserAgentInfo GetForSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Bounded capacity check
        if (_sessions.Count >= _maxSessions && !_sessions.ContainsKey(sessionId))
        {
            TrimSessions();
        }

        var lazy = _sessions.GetOrAdd(
            sessionId,
            _ => new Lazy<UserAgentInfo>(() => Next(), LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    /// <summary>
    /// Attempts to retrieve an existing user agent for the specified session without creating a new one.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="info">When this method returns, contains the pinned user agent if found; otherwise, null.</param>
    /// <returns><see langword="true"/> if a session exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetSession(string sessionId, out UserAgentInfo? info)
    {
        if (_sessions.TryGetValue(sessionId, out var lazy) && lazy.IsValueCreated)
        {
            info = lazy.Value;
            return true;
        }

        info = null;
        return false;
    }

    /// <summary>
    /// Removes a persistent session assignment.
    /// </summary>
    /// <param name="sessionId">The session identifier to remove.</param>
    /// <returns><see langword="true"/> if the session was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveSession(string sessionId) =>
        _sessions.TryRemove(sessionId, out _);

    /// <summary>
    /// Clears all stored persistent sessions.
    /// </summary>
    public void ClearSessions() => _sessions.Clear();

    /// <summary>
    /// Resets the rotator state (round-robin counter and shuffled deck).
    /// Does not clear persistent sessions unless <see cref="ClearSessions"/> is also called.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _currentIndex, -1);

        lock (_shuffleSync)
        {
            _shuffledPosition = 0;
            ShuffleIndicesInternal();
        }

        lock (_noRepeatSync)
        {
            _lastPickedIndex = -1;
        }
    }

    // ---- Lock-Free & Synchronized Picking Strategies --------------------------

    /// <summary>
    /// Lock-free atomic round-robin counter.
    /// </summary>
    private int PickRoundRobin()
    {
        int next = Interlocked.Increment(ref _currentIndex);
        return (int)((uint)next % (uint)_pool.Length);
    }

    /// <summary>
    /// Lock-free market-weighted random selection when using Random.Shared; synchronized when using custom instance.
    /// </summary>
    private int PickRandom()
    {
        if (_pool.Length <= 1) return 0;

        double target;
        if (_isSharedRandom)
        {
            target = Random.Shared.NextDouble() * _totalWeight;
        }
        else
        {
            lock (_randomSync)
            {
                target = _random.NextDouble() * _totalWeight;
            }
        }

        int idx = Array.BinarySearch(_cumulativeWeights, target);
        if (idx < 0) idx = ~idx;
        if (idx >= _pool.Length) idx = _pool.Length - 1;
        return idx;
    }

    /// <summary>
    /// Amortized lock-free: atomic increment for all requests within a cycle;
    /// synchronization only occurs at the cycle boundary to reshuffle.
    /// </summary>
    private int PickShuffled()
    {
        while (true)
        {
            int pos = Interlocked.Increment(ref _shuffledPosition) - 1;

            if (pos < _shuffledIndices.Length)
            {
                return _shuffledIndices[pos];
            }

            // Cycle exhausted — synchronize reshuffle
            lock (_shuffleSync)
            {
                if (_shuffledPosition >= _shuffledIndices.Length)
                {
                    ShuffleIndicesInternal();
                    _shuffledPosition = 0;
                }
            }
        }
    }

    /// <summary>
    /// Targeted lightweight lock for NoImmediateRepeat to safely track the previous index.
    /// </summary>
    private int PickNoImmediateRepeat()
    {
        if (_pool.Length <= 1)
        {
            return 0;
        }

        lock (_noRepeatSync)
        {
            int candidate = _isSharedRandom ? Random.Shared.Next(_pool.Length - 1) : _random.Next(_pool.Length - 1);
            if (_lastPickedIndex >= 0 && candidate >= _lastPickedIndex)
            {
                candidate++;
            }

            _lastPickedIndex = candidate;
            return candidate;
        }
    }

    private void ShuffleIndicesInternal()
    {
        int n = _shuffledIndices.Length;
        while (n > 1)
        {
            int k = _isSharedRandom ? Random.Shared.Next(n--) : _random.Next(n--);
            (_shuffledIndices[n], _shuffledIndices[k]) = (_shuffledIndices[k], _shuffledIndices[n]);
        }
    }

    private void TrimSessions()
    {
        // Simple bounded capacity eviction: trim 10% of items when limit exceeded
        int targetEvictions = Math.Max(1, _maxSessions / 10);
        int evicted = 0;

        foreach (var key in _sessions.Keys)
        {
            if (_sessions.TryRemove(key, out _))
            {
                evicted++;
                if (evicted >= targetEvictions) break;
            }
        }
    }

    private static ImmutableArray<UserAgentInfo> ResolvePool(Browser? browser, DeviceType? device, OperatingSystemType? os)
    {
        if (UaIndex.TryGetPool(browser, device, os, out var pool) && pool.Length > 0)
        {
            return pool.Entries;
        }

        return ImmutableArray.Create(UaDatabase.DefaultEntry);
    }

    private static ImmutableArray<UserAgentInfo> FilterEntries(
        ImmutableArray<UserAgentInfo> source,
        Func<UserAgentInfo, bool>? filter)
    {
        if (filter is null) return source;

        var builder = ImmutableArray.CreateBuilder<UserAgentInfo>();
        foreach (var entry in source)
        {
            if (filter(entry))
            {
                builder.Add(entry);
            }
        }

        return builder.Count > 0 ? builder.ToImmutable() : ImmutableArray.Create(UaDatabase.DefaultEntry);
    }
}
