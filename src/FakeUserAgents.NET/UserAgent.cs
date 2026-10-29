using Dotnet.FakeUserAgents.Internal;
using System.Diagnostics.CodeAnalysis;

namespace Dotnet.FakeUserAgents;

/// <summary>
/// Provides realistic browser user agent strings from a built-in pre-indexed database.
/// Supports both market-weighted selection (reflecting real-world web traffic share) and uniform random selection.
/// Filtering methods never throw for "no match" — they degrade gracefully.
/// Use <see cref="TryGet(Browser?, DeviceType?, OperatingSystemType?, out UserAgentInfo?)"/> when you need strict matching instead.
/// </summary>
public sealed class UserAgent
{
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAgent"/> class backed by a thread-safe shared random generator (<see cref="Random.Shared"/>).
    /// </summary>
    public UserAgent() : this(Random.Shared) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAgent"/> class with a deterministic sequence seeded by <paramref name="seed"/>.
    /// Useful for repeatable unit tests and benchmarks.
    /// </summary>
    /// <param name="seed">The pseudo-random number generator seed.</param>
    public UserAgent(int seed) : this(new Random(seed)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAgent"/> class backed by a custom random number generator.
    /// </summary>
    /// <param name="random">The random generator to use, or <see langword="null"/> to use <see cref="Random.Shared"/>. Must be thread-safe if shared across threads.</param>
    public UserAgent(Random? random) => _random = random ?? Random.Shared;

    // ---- Random ---------------------------------------------------------------

    /// <summary>
    /// Returns a random user agent from the database in O(1)/O(log K) time with 0 allocations.
    /// </summary>
    /// <param name="weighted">If <see langword="true"/> (default), picks proportional to real-world browser market share; otherwise picks uniformly.</param>
    /// <returns>A selected <see cref="UserAgentInfo"/> instance.</returns>
    public UserAgentInfo GetRandom(bool weighted = true) =>
        weighted ? UaIndex.All.PickWeighted(_random) : UaIndex.All.PickRandom(_random);

    /// <summary>
    /// Returns a random user agent filtered by device category.
    /// </summary>
    /// <param name="device">The device category (e.g., Desktop, Mobile, Tablet).</param>
    /// <param name="weighted">If <see langword="true"/> (default), picks proportional to market weight.</param>
    /// <returns>A matching <see cref="UserAgentInfo"/> instance, degrading gracefully if necessary.</returns>
    public UserAgentInfo GetRandom(DeviceType device, bool weighted = true) =>
        Get(device: device, weighted: weighted);

    /// <summary>
    /// Returns a random user agent filtered by operating system.
    /// </summary>
    /// <param name="os">The operating system family (e.g., Windows, MacOS, Android).</param>
    /// <param name="weighted">If <see langword="true"/> (default), picks proportional to market weight.</param>
    /// <returns>A matching <see cref="UserAgentInfo"/> instance, degrading gracefully if necessary.</returns>
    public UserAgentInfo GetRandom(OperatingSystemType os, bool weighted = true) =>
        Get(os: os, weighted: weighted);

    /// <summary>
    /// Returns a random user agent filtered by both device category and operating system.
    /// </summary>
    /// <param name="device">The device category (e.g., Mobile, Desktop).</param>
    /// <param name="os">The operating system family (e.g., Android, Windows).</param>
    /// <param name="weighted">If <see langword="true"/> (default), picks proportional to market weight.</param>
    /// <returns>A matching <see cref="UserAgentInfo"/> instance, degrading gracefully if necessary.</returns>
    public UserAgentInfo GetRandom(DeviceType device, OperatingSystemType os, bool weighted = true) =>
        Get(device: device, os: os, weighted: weighted);

    // ---- By browser -----------------------------------------------------------

    /// <summary>
    /// Returns a user agent for the specified browser.
    /// </summary>
    /// <param name="browser">The browser family.</param>
    /// <param name="weighted">If <see langword="true"/> (default), picks proportional to version recency and market weight.</param>
    /// <returns>A matching <see cref="UserAgentInfo"/> instance.</returns>
    public UserAgentInfo GetByBrowser(Browser browser, bool weighted = true) =>
        Get(browser: browser, weighted: weighted);

    /// <summary>
    /// Returns a user agent for the specified browser and device category.
    /// </summary>
    public UserAgentInfo GetByBrowser(Browser browser, DeviceType device, bool weighted = true) =>
        Get(browser, device, weighted: weighted);

    /// <summary>
    /// Returns a user agent for the specified browser and operating system.
    /// </summary>
    public UserAgentInfo GetByBrowser(Browser browser, OperatingSystemType os, bool weighted = true) =>
        Get(browser, os: os, weighted: weighted);

    /// <summary>
    /// Returns a user agent matching the specified browser, device category, and operating system.
    /// </summary>
    public UserAgentInfo GetByBrowser(Browser browser, DeviceType device, OperatingSystemType os, bool weighted = true) =>
        Get(browser, device, os, weighted: weighted);

    /// <summary>
    /// Returns a user agent by browser name (case-insensitive, e.g. "chrome", "Firefox").
    /// Unknown or unparseable browser names fall back to a random entry.
    /// </summary>
    public UserAgentInfo GetByBrowser(string browser, bool weighted = true) =>
        TryParseBrowser(browser, out var b) ? GetByBrowser(b, weighted) : GetRandom(weighted);

    /// <summary>
    /// Returns a user agent by browser name and device category.
    /// </summary>
    public UserAgentInfo GetByBrowser(string browser, DeviceType device, bool weighted = true) =>
        TryParseBrowser(browser, out var b) ? GetByBrowser(b, device, weighted) : GetRandom(device, weighted);

    /// <summary>
    /// Returns a user agent by browser name and operating system.
    /// </summary>
    public UserAgentInfo GetByBrowser(string browser, OperatingSystemType os, bool weighted = true) =>
        TryParseBrowser(browser, out var b) ? GetByBrowser(b, os, weighted) : GetRandom(os, weighted);

    /// <summary>
    /// Returns a user agent by browser name, device category, and operating system.
    /// </summary>
    public UserAgentInfo GetByBrowser(string browser, DeviceType device, OperatingSystemType os, bool weighted = true) =>
        TryParseBrowser(browser, out var b) ? GetByBrowser(b, device, os, weighted) : Get(device: device, os: os, weighted: weighted);

    // ---- By device / OS -------------------------------------------------------

    /// <summary>
    /// Returns a random user agent matching the specified device category.
    /// </summary>
    public UserAgentInfo GetByDevice(DeviceType device, bool weighted = true) =>
        Get(device: device, weighted: weighted);

    /// <summary>
    /// Returns a random user agent matching the specified operating system.
    /// </summary>
    public UserAgentInfo GetByOs(OperatingSystemType os, bool weighted = true) =>
        Get(os: os, weighted: weighted);

    // ---- General --------------------------------------------------------------

    /// <summary>
    /// Returns a matching entry using precomputed indexes. If the exact combination doesn't exist
    /// (e.g. Safari on Windows), it degrades gracefully: relaxes OS first, then device, then browser — finally returning anything.
    /// Operates in O(1)/O(log K) time with 0 bytes allocated.
    /// </summary>
    /// <param name="browser">Optional browser filter.</param>
    /// <param name="device">Optional device category filter.</param>
    /// <param name="os">Optional operating system filter.</param>
    /// <param name="weighted">If <see langword="true"/> (default), picks proportional to market weight; otherwise uniform.</param>
    /// <returns>A <see cref="UserAgentInfo"/> matching the request as closely as possible.</returns>
    public UserAgentInfo Get(
        Browser? browser = null,
        DeviceType? device = null,
        OperatingSystemType? os = null,
        bool weighted = true)
    {
        // 1. Exact match
        if (UaIndex.TryGetPool(browser, device, os, out var pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 2. Relax OS
        if (browser is not null && os is not null && UaIndex.TryGetPool(browser, device, null, out pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 3. Relax device
        if (browser is not null && device is not null && UaIndex.TryGetPool(browser, null, os, out pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 4. Browser only
        if (browser is not null && UaIndex.TryGetPool(browser, null, null, out pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 5. Device + OS
        if (device is not null && os is not null && UaIndex.TryGetPool(null, device, os, out pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 6. Device only
        if (device is not null && UaIndex.TryGetPool(null, device, null, out pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 7. OS only
        if (os is not null && UaIndex.TryGetPool(null, null, os, out pool) && pool.Length > 0)
        {
            return weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
        }

        // 8. Anything at all
        return GetRandom(weighted);
    }

    /// <summary>
    /// Performs an O(1) strict match without fallback using the precomputed pool index.
    /// </summary>
    /// <param name="browser">Optional browser filter.</param>
    /// <param name="device">Optional device category filter.</param>
    /// <param name="os">Optional operating system filter.</param>
    /// <param name="info">When this method returns, contains the matched <see cref="UserAgentInfo"/> if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if an exact matching user agent was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(Browser? browser, DeviceType? device, OperatingSystemType? os, [NotNullWhen(true)] out UserAgentInfo? info) =>
        TryGet(browser, device, os, weighted: true, out info);

    /// <summary>
    /// Performs an O(1) strict match without fallback using the precomputed pool index with configurable weighting.
    /// </summary>
    public bool TryGet(Browser? browser, DeviceType? device, OperatingSystemType? os, bool weighted, [NotNullWhen(true)] out UserAgentInfo? info)
    {
        if (UaIndex.TryGetPool(browser, device, os, out var pool) && pool.Length > 0)
        {
            info = weighted ? pool.PickWeighted(_random) : pool.PickRandom(_random);
            return true;
        }

        info = null;
        return false;
    }

    // ---- Dictionary helpers ---------------------------------------------------

    /// <summary>
    /// Returns a random user agent formatted as a dictionary of metadata key-value pairs.
    /// Note: Allocates a new dictionary on each call. For high-throughput scenarios, use <see cref="GetRandom(bool)"/> or <see cref="UserAgentInfo.UserAgent"/>.
    /// </summary>
    public Dictionary<string, string> GetRandomAsDictionary(bool weighted = true) =>
        GetRandom(weighted).ToDictionary();

    /// <summary>
    /// Returns a user agent matching the specified filters formatted as a dictionary of metadata key-value pairs.
    /// Note: Allocates a new dictionary on each call.
    /// </summary>
    public Dictionary<string, string> GetAsDictionary(
        Browser? browser = null,
        DeviceType? device = null,
        OperatingSystemType? os = null,
        bool weighted = true) =>
        Get(browser, device, os, weighted).ToDictionary();

    // ---- Helpers --------------------------------------------------------------

    /// <summary>
    /// Parses a browser name case-insensitively ("chrome", "Firefox", "EDGE", ...).
    /// </summary>
    public static bool TryParseBrowser(string? name, out Browser browser)
    {
        if (Enum.TryParse(name?.Trim(), ignoreCase: true, out browser) && Enum.IsDefined(browser))
        {
            return true;
        }

        browser = default;
        return false;
    }
}