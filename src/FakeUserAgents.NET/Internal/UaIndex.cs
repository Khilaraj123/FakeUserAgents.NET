using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Dotnet.FakeUserAgents.Internal;

/// <summary>
/// Precomputes and caches immutable slices of the user agent database with cumulative weight indexes.
/// Provides O(1) pool resolution and O(log K) zero-allocation market-weighted random selection
/// across any combination of browser, device, and operating system filters.
/// </summary>
internal static class UaIndex
{
    public readonly struct IndexedPool : IReadOnlyList<UserAgentInfo>
    {
        public static readonly IndexedPool Empty = new(ImmutableArray<UserAgentInfo>.Empty);

        public ImmutableArray<UserAgentInfo> Entries { get; }
        public double[] CumulativeWeights { get; }
        public double TotalWeight { get; }
        public int Length => Entries.IsDefault ? 0 : Entries.Length;
        public int Count => Length;
        public bool IsDefaultOrEmpty => Entries.IsDefaultOrEmpty;

        public UserAgentInfo this[int index] => Entries[index];

        public IndexedPool(ImmutableArray<UserAgentInfo> entries)
        {
            Entries = entries;
            if (entries.IsDefaultOrEmpty)
            {
                CumulativeWeights = [];
                TotalWeight = 0;
                return;
            }

            var cumulative = new double[entries.Length];
            double sum = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                sum += entries[i].Weight;
                cumulative[i] = sum;
            }

            CumulativeWeights = cumulative;
            TotalWeight = sum;
        }

        public UserAgentInfo PickRandom(Random random) =>
            Length == 0 ? throw new InvalidOperationException("Pool is empty.") : Entries[random.Next(Entries.Length)];

        public UserAgentInfo PickWeighted(Random random)
        {
            if (Length == 0) throw new InvalidOperationException("Pool is empty.");
            if (Entries.Length <= 1) return Entries[0];

            double target = random.NextDouble() * TotalWeight;
            int idx = Array.BinarySearch(CumulativeWeights, target);
            if (idx < 0) idx = ~idx;
            if (idx >= Entries.Length) idx = Entries.Length - 1;
            return Entries[idx];
        }

        public ImmutableArray<UserAgentInfo>.Enumerator GetEnumerator() =>
            Entries.IsDefault ? ImmutableArray<UserAgentInfo>.Empty.GetEnumerator() : Entries.GetEnumerator();

        IEnumerator<UserAgentInfo> IEnumerable<UserAgentInfo>.GetEnumerator() =>
            ((IEnumerable<UserAgentInfo>)(Entries.IsDefault ? ImmutableArray<UserAgentInfo>.Empty : Entries)).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            ((System.Collections.IEnumerable)(Entries.IsDefault ? ImmutableArray<UserAgentInfo>.Empty : Entries)).GetEnumerator();
    }

    private static readonly FrozenDictionary<(Browser?, DeviceType?, OperatingSystemType?), IndexedPool> s_pools;
    private static readonly IndexedPool s_allEntriesPool;

    static UaIndex()
    {
        var entries = UaDatabase.Entries;
        s_allEntriesPool = new IndexedPool(entries);

        var map = new Dictionary<(Browser?, DeviceType?, OperatingSystemType?), IndexedPool>();

        var browsers = Enum.GetValues<Browser>().Cast<Browser?>().Concat([null]);
        var devices = Enum.GetValues<DeviceType>().Cast<DeviceType?>().Concat([null]);
        var osList = Enum.GetValues<OperatingSystemType>().Cast<OperatingSystemType?>().Concat([null]);

        foreach (var b in browsers)
        {
            foreach (var d in devices)
            {
                foreach (var o in osList)
                {
                    var matches = entries.Where(e =>
                        (b is null || e.Browser == b.Value) &&
                        (d is null || e.DeviceType == d.Value) &&
                        (o is null || e.Os == o.Value)).ToImmutableArray();

                    if (matches.Length > 0)
                    {
                        map[(b, d, o)] = new IndexedPool(matches);
                    }
                }
            }
        }

        s_pools = map.ToFrozenDictionary();
    }

    /// <summary>
    /// Gets the indexed pool containing all user agents in the database.
    /// </summary>
    public static IndexedPool All => s_allEntriesPool;

    /// <summary>
    /// Attempts to retrieve a precomputed pool for the specified filter combination.
    /// </summary>
    public static bool TryGetPool(
        Browser? browser,
        DeviceType? device,
        OperatingSystemType? os,
        out IndexedPool pool) =>
        s_pools.TryGetValue((browser, device, os), out pool);

    /// <summary>
    /// Gets the precomputed pool for a specific browser, or an empty pool if none exist.
    /// </summary>
    public static IndexedPool GetByBrowser(Browser browser) =>
        s_pools.TryGetValue((browser, null, null), out var pool) ? pool : IndexedPool.Empty;

    /// <summary>
    /// Gets the precomputed pool for a specific device category, or an empty pool if none exist.
    /// </summary>
    public static IndexedPool GetByDevice(DeviceType device) =>
        s_pools.TryGetValue((null, device, null), out var pool) ? pool : IndexedPool.Empty;

    /// <summary>
    /// Gets the precomputed pool for a specific operating system, or an empty pool if none exist.
    /// </summary>
    public static IndexedPool GetByOs(OperatingSystemType os) =>
        s_pools.TryGetValue((null, null, os), out var pool) ? pool : IndexedPool.Empty;
}
