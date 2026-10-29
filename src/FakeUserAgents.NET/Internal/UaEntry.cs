using System.Text.Json.Serialization;

namespace Dotnet.FakeUserAgents.Internal;

/// <summary>
/// Data transfer object used for deserializing raw records from the embedded <c>useragents.json</c> file.
/// </summary>
internal sealed class UaEntry
{
    /// <summary>Raw user agent string.</summary>
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>Browser name string (e.g. "Chrome", "Firefox").</summary>
    [JsonPropertyName("browser")]
    public string Browser { get; set; } = string.Empty;

    /// <summary>Browser version number string.</summary>
    [JsonPropertyName("browserVersion")]
    public string BrowserVersion { get; set; } = string.Empty;

    /// <summary>Device category string (e.g. "desktop", "mobile", "tablet").</summary>
    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>Operating system string (e.g. "Windows", "MacOS", "Android").</summary>
    [JsonPropertyName("os")]
    public string Os { get; set; } = string.Empty;

    /// <summary>Operating system version string.</summary>
    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>Hardware model string if applicable, or null.</summary>
    [JsonPropertyName("device")]
    public string? Device { get; set; }

    /// <summary>Relative market weight.</summary>
    [JsonPropertyName("weight")]
    public double? Weight { get; set; }
}
