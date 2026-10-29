using System.Collections.Concurrent;

namespace Dotnet.FakeUserAgents;

/// <summary>
/// High-performance, memory-bounded session persistence manager for user agents.
/// Ensures web clients and crawler sessions consistently retain the same user agent across requests
/// with zero-allocation cache hits, bounded memory capacity, atomic session creation, and automatic operation-based cleanup.
/// </summary>
public sealed class UserAgentSessionManager
{
    private sealed class SessionEntry
    {
        public required UserAgentInfo Info { get; init; }
        public long LastAccessTicks;
    }

    private readonly ConcurrentDictionary<string, Lazy<SessionEntry>> _sessions = new(StringComparer.Ordinal);
    private readonly UserAgent _defaultGenerator;
    private readonly TimeSpan? _slidingExpiration;
    private readonly long _expirationTicks;
    private readonly int _maxSessions;
    private int _operationCounter;

    /// <summary>
    /// Gets the count of currently active tracked sessions.
    /// </summary>
    public int Count => _sessions.Count;

    /// <summary>
    /// Gets the maximum allowed session capacity.
    /// </summary>
    public int MaxSessions => _maxSessions;

    /// <summary>
    /// Initializes a new instance of <see cref="UserAgentSessionManager"/>.
    /// </summary>
    /// <param name="slidingExpiration">Optional duration after which inactive sessions expire and are purged.</param>
    /// <param name="maxSessions">Maximum session entries to store before triggering capacity evictions (default: 10,000).</param>
    /// <param name="generator">Optional <see cref="UserAgent"/> generator used when creating new sessions.</param>
    public UserAgentSessionManager(
        TimeSpan? slidingExpiration = null,
        int maxSessions = 10_000,
        UserAgent? generator = null)
    {
        _slidingExpiration = slidingExpiration;
        _expirationTicks = slidingExpiration?.Ticks ?? 0;
        _maxSessions = Math.Max(100, maxSessions);
        _defaultGenerator = generator ?? new UserAgent();
    }

    /// <summary>
    /// Retrieves the user agent assigned to the session, or creates a new assignment atomically.
    /// Allocates 0 bytes on cache hits.
    /// </summary>
    /// <param name="sessionId">The unique session identifier.</param>
    /// <param name="customGenerator">Optional generator function to produce a user agent if none exists.</param>
    /// <returns>The <see cref="UserAgentInfo"/> associated with this session.</returns>
    public UserAgentInfo GetOrCreate(string sessionId, Func<UserAgentInfo>? customGenerator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Opportunistic automated cleanup every 1,024 operations (no background timer needed)
        if ((Interlocked.Increment(ref _operationCounter) & 1023) == 0)
        {
            PurgeExpired();
        }

        long nowTicks = DateTime.UtcNow.Ticks;

        // Try fast path lookup
        if (_sessions.TryGetValue(sessionId, out var existingLazy))
        {
            var entry = existingLazy.Value;
            if (_expirationTicks == 0 || (nowTicks - Volatile.Read(ref entry.LastAccessTicks)) <= _expirationTicks)
            {
                // Zero-allocation timestamp update on hit
                Volatile.Write(ref entry.LastAccessTicks, nowTicks);
                return entry.Info;
            }

            // Expired — remove stale entry
            _sessions.TryRemove(sessionId, out _);
        }

        // Bounded capacity protection
        if (_sessions.Count >= _maxSessions)
        {
            TrimExcess();
        }

        // Atomic creation via Lazy<SessionEntry> ensures the generator runs strictly once even under concurrent races
        var createdLazy = _sessions.GetOrAdd(
            sessionId,
            _ => new Lazy<SessionEntry>(() =>
            {
                var info = customGenerator is not null ? customGenerator() : _defaultGenerator.GetRandom();
                return new SessionEntry
                {
                    Info = info,
                    LastAccessTicks = DateTime.UtcNow.Ticks
                };
            }, LazyThreadSafetyMode.ExecutionAndPublication));

        var finalEntry = createdLazy.Value;
        Volatile.Write(ref finalEntry.LastAccessTicks, nowTicks);
        return finalEntry.Info;
    }

    /// <summary>
    /// Attempts to retrieve an existing user agent without generating a new one.
    /// </summary>
    /// <param name="sessionId">The unique session identifier.</param>
    /// <param name="info">When this method returns, contains the user agent if active; otherwise, null.</param>
    /// <returns><see langword="true"/> if a valid session was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string sessionId, out UserAgentInfo? info)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        long nowTicks = DateTime.UtcNow.Ticks;

        if (_sessions.TryGetValue(sessionId, out var lazy) && lazy.IsValueCreated)
        {
            var entry = lazy.Value;
            if (_expirationTicks == 0 || (nowTicks - Volatile.Read(ref entry.LastAccessTicks)) <= _expirationTicks)
            {
                Volatile.Write(ref entry.LastAccessTicks, nowTicks);
                info = entry.Info;
                return true;
            }

            _sessions.TryRemove(sessionId, out _);
        }

        info = null;
        return false;
    }

    /// <summary>
    /// Explicitly sets or overrides the user agent for a given session.
    /// </summary>
    /// <param name="sessionId">The unique session identifier.</param>
    /// <param name="info">The user agent to associate with the session.</param>
    public void Set(string sessionId, UserAgentInfo info)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(info);

        var entry = new SessionEntry
        {
            Info = info,
            LastAccessTicks = DateTime.UtcNow.Ticks
        };

        _sessions[sessionId] = new Lazy<SessionEntry>(entry);
    }

    /// <summary>
    /// Removes a persistent session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns><see langword="true"/> if the session was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(string sessionId) =>
        _sessions.TryRemove(sessionId, out _);

    /// <summary>
    /// Purges all expired sessions if a sliding expiration is configured.
    /// </summary>
    /// <returns>The number of expired sessions removed.</returns>
    public int PurgeExpired()
    {
        if (_expirationTicks == 0) return 0;

        int removed = 0;
        long threshold = DateTime.UtcNow.Ticks - _expirationTicks;

        foreach (var (key, lazy) in _sessions)
        {
            if (lazy.IsValueCreated && Volatile.Read(ref lazy.Value.LastAccessTicks) < threshold)
            {
                if (_sessions.TryRemove(key, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Evicts excess entries when max capacity is reached.
    /// </summary>
    private void TrimExcess()
    {
        // First try purging expired
        int purged = PurgeExpired();
        if (purged > 0 && _sessions.Count < _maxSessions)
        {
            return;
        }

        // Still over capacity: evict oldest 10%
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

    /// <summary>
    /// Clears all stored sessions.
    /// </summary>
    public void Clear() => _sessions.Clear();
}
