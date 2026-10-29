namespace Dotnet.FakeUserAgents;

/// <summary>
/// Operating system families. Named <c>OperatingSystemType</c> to avoid clashing with <c>System.OperatingSystem</c>.
/// </summary>
public enum OperatingSystemType
{
    /// <summary>Microsoft Windows operating system family.</summary>
    Windows,

    /// <summary>Apple macOS desktop operating system.</summary>
    MacOS,

    /// <summary>Linux-based desktop/server distributions.</summary>
    Linux,

    /// <summary>Google ChromeOS operating system.</summary>
    ChromeOS,

    /// <summary>Google Android mobile and tablet operating system.</summary>
    Android,

    /// <summary>Apple iOS / iPadOS mobile and tablet operating system.</summary>
    IOS
}
