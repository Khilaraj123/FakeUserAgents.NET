namespace Dotnet.FakeUserAgents;

/// <summary>
/// Represents a user agent string along with structured metadata describing the browser, device, and operating system.
/// </summary>
public sealed record UserAgentInfo
{
    /// <summary>
    /// Gets the raw user agent header string.
    /// </summary>
    public required string UserAgent { get; init; }

    /// <summary>
    /// Gets the browser family (e.g., Chrome, Firefox, Safari).
    /// </summary>
    public required Browser Browser { get; init; }

    /// <summary>
    /// Gets the version number of the browser (e.g., "124.0.0.0").
    /// </summary>
    public required string BrowserVersion { get; init; }

    /// <summary>
    /// Gets the device category (Desktop, Mobile, or Tablet).
    /// </summary>
    public required DeviceType DeviceType { get; init; }

    /// <summary>
    /// Gets the operating system family (e.g., Windows, MacOS, Android).
    /// </summary>
    public required OperatingSystemType Os { get; init; }

    /// <summary>
    /// Gets the version string of the operating system (e.g., "10/11", "14", "17.4").
    /// </summary>
    public required string OsVersion { get; init; }

    /// <summary>
    /// Gets the hardware device model where known (e.g., "Pixel 8", "iPhone"); otherwise <see langword="null"/>.
    /// </summary>
    public string? Device { get; init; }

    /// <summary>
    /// Gets the relative market weight of this user agent profile (higher weight = more prevalent in real traffic).
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Converts the user agent metadata into a dictionary.
    /// </summary>
    /// <remarks>
    /// Contains the following keys:
    /// <c>userAgent</c>, <c>browser</c>, <c>browserVersion</c>, <c>deviceType</c>, <c>os</c>, <c>osVersion</c>, <c>device</c>, <c>weight</c>.
    /// </remarks>
    /// <returns>A dictionary containing all user agent properties mapped to string values.</returns>
    public Dictionary<string, string> ToDictionary() => new()
    {
        ["userAgent"] = UserAgent,
        ["browser"] = Browser.ToString(),
        ["browserVersion"] = BrowserVersion,
        ["deviceType"] = DeviceType.ToString(),
        ["os"] = Os.ToString(),
        ["osVersion"] = OsVersion,
        ["device"] = Device ?? string.Empty,
        ["weight"] = Weight.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// Returns the raw user agent string.
    /// </summary>
    /// <returns>The raw user agent string.</returns>
    public override string ToString() => UserAgent;
}