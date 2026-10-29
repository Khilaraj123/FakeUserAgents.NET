using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotnet.FakeUserAgents.Internal;

/// <summary>
/// Internal data repository that loads and caches user agent definitions from the embedded JSON resource.
/// Includes a resilient fallback catalog to ensure the library remains operational even if the resource is corrupted or missing.
/// </summary>
internal static class UaDatabase
{
    private const string ResourceName = "Dotnet.FakeUserAgents.Data.useragents.json";

    /// <summary>
    /// Thread-safe lazy initializer for loading the user agent database on first access.
    /// </summary>
    private static readonly Lazy<ImmutableArray<UserAgentInfo>> s_entries =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets all parsed user agent entries currently loaded in memory.
    /// </summary>
    public static ImmutableArray<UserAgentInfo> Entries => s_entries.Value;

    /// <summary>
    /// Minimal hardcoded collection of modern user agents used as a fallback if the embedded resource is missing or corrupt.
    /// Ensures methods never throw or return empty lists under unexpected environment failures.
    /// </summary>
    internal static ImmutableArray<UserAgentInfo> FallbackEntries { get; } = ImmutableArray.Create(
        new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36",
            Browser = Browser.Chrome,
            BrowserVersion = "145.0.0.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11",
            Device = null,
            Weight = 30.0
        },
        new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:145.0) Gecko/20100101 Firefox/145.0",
            Browser = Browser.Firefox,
            BrowserVersion = "145.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11",
            Device = null,
            Weight = 3.0
        },
        new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.3 Safari/605.1.15",
            Browser = Browser.Safari,
            BrowserVersion = "18.3",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.MacOS,
            OsVersion = "15.3",
            Device = null,
            Weight = 10.0
        },
        new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Mobile Safari/537.36",
            Browser = Browser.Chrome,
            BrowserVersion = "145.0.0.0",
            DeviceType = DeviceType.Mobile,
            Os = OperatingSystemType.Android,
            OsVersion = "10",
            Device = "K",
            Weight = 35.0
        },
        new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_3 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.3 Mobile/15E148 Safari/604.1",
            Browser = Browser.Safari,
            BrowserVersion = "18.3",
            DeviceType = DeviceType.Mobile,
            Os = OperatingSystemType.IOS,
            OsVersion = "18.3",
            Device = "iPhone",
            Weight = 12.0
        });

    /// <summary>
    /// Gets a guaranteed safe default entry (Chrome on Windows Desktop).
    /// </summary>
    public static UserAgentInfo DefaultEntry => FallbackEntries[0];

    /// <summary>
    /// Reads and deserializes the embedded <c>useragents.json</c> stream into an immutable array of <see cref="UserAgentInfo"/>.
    /// Gracefully recovers from any missing resources or serialization failures by returning <see cref="FallbackEntries"/>.
    /// </summary>
    private static ImmutableArray<UserAgentInfo> Load()
    {
        try
        {
            var assembly = typeof(UaDatabase).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName)
                ?? (assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("useragents.json", StringComparison.OrdinalIgnoreCase)) is { } fallbackName
                    ? assembly.GetManifestResourceStream(fallbackName)
                    : null);
            if (stream is null) return FallbackEntries;

            var raw = JsonSerializer.Deserialize(stream, UaJsonContext.Default.UaEntryArray);
            if (raw is null || raw.Length == 0) return FallbackEntries;

            var builder = ImmutableArray.CreateBuilder<UserAgentInfo>(raw.Length);
            foreach (var e in raw)
            {
                if (!Enum.TryParse<Browser>(e.Browser, ignoreCase: true, out var browser) ||
                    !Enum.TryParse<DeviceType>(e.DeviceType, ignoreCase: true, out var device) ||
                    !Enum.TryParse<OperatingSystemType>(e.Os, ignoreCase: true, out var os))
                {
                    continue; // skip malformed rows instead of failing the whole database
                }

                builder.Add(new UserAgentInfo
                {
                    UserAgent = e.UserAgent,
                    Browser = browser,
                    BrowserVersion = e.BrowserVersion,
                    DeviceType = device,
                    Os = os,
                    OsVersion = e.OsVersion,
                    Device = string.IsNullOrWhiteSpace(e.Device) ? null : e.Device,
                    Weight = e.Weight is > 0 ? e.Weight.Value : 1.0
                });
            }

            return builder.Count > 0 ? builder.ToImmutable() : FallbackEntries;
        }
        catch
        {
            // Anything goes wrong (resource missing, bad JSON, ...) → still functional with safe defaults.
            return FallbackEntries;
        }
    }
}
