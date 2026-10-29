using System.Text.RegularExpressions;

namespace Dotnet.FakeUserAgents;

/// <summary>
/// Represents the result of validating a <see cref="UserAgentInfo"/> instance or collection.
/// </summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>Gets a successful validation result.</summary>
    public static readonly ValidationResult Success = new(true, Array.Empty<string>());

    /// <summary>Creates a failed validation result with the specified error messages.</summary>
    public static ValidationResult Failure(params string[] errors) => new(false, errors);

    /// <summary>Creates a failed validation result with the specified error messages.</summary>
    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());

    /// <summary>Returns a formatted string summary of errors, or "Valid".</summary>
    public string Summary => IsValid ? "Valid" : string.Join("; ", Errors);

    /// <summary>Returns the summary string.</summary>
    public override string ToString() => Summary;
}

/// <summary>
/// Validates user agent entries to ensure structural integrity and strict consistency
/// across browser family, device category, operating system, and the raw User-Agent header string.
/// </summary>
public static class UserAgentValidator
{
    /// <summary>
    /// Validates an individual <see cref="UserAgentInfo"/> entry.
    /// </summary>
    /// <param name="info">The user agent entry to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the entry is valid and containing any violations.</returns>
    public static ValidationResult Validate(UserAgentInfo? info)
    {
        if (info is null)
        {
            return ValidationResult.Failure("UserAgentInfo instance cannot be null.");
        }

        var errors = new List<string>();

        // 1. Basic non-empty checks
        if (string.IsNullOrWhiteSpace(info.UserAgent) || info.UserAgent.Length < 20)
        {
            errors.Add($"UserAgent string is invalid or too short (length: {info.UserAgent?.Length ?? 0}).");
        }

        if (string.IsNullOrWhiteSpace(info.BrowserVersion))
        {
            errors.Add("BrowserVersion cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(info.OsVersion))
        {
            errors.Add("OsVersion cannot be empty.");
        }

        if (info.Weight <= 0 || double.IsNaN(info.Weight) || double.IsInfinity(info.Weight))
        {
            errors.Add($"Weight must be a positive finite number, but had {info.Weight}.");
        }

        // 2. Enum definitions
        if (!Enum.IsDefined(typeof(Browser), info.Browser))
        {
            errors.Add($"Invalid Browser enum value: {info.Browser}.");
        }

        if (!Enum.IsDefined(typeof(DeviceType), info.DeviceType))
        {
            errors.Add($"Invalid DeviceType enum value: {info.DeviceType}.");
        }

        if (!Enum.IsDefined(typeof(OperatingSystemType), info.Os))
        {
            errors.Add($"Invalid OperatingSystemType enum value: {info.Os}.");
        }

        // 3. OS vs DeviceType consistency
        switch (info.Os)
        {
            case OperatingSystemType.Windows:
            case OperatingSystemType.MacOS:
            case OperatingSystemType.Linux:
            case OperatingSystemType.ChromeOS:
                if (info.DeviceType != DeviceType.Desktop)
                {
                    errors.Add($"{info.Os} must have DeviceType.Desktop, but had {info.DeviceType}.");
                }
                if (!string.IsNullOrEmpty(info.Device))
                {
                    errors.Add($"Desktop OS {info.Os} should not have a hardware device model specified, but had '{info.Device}'.");
                }
                break;

            case OperatingSystemType.Android:
                if (info.DeviceType is not (DeviceType.Mobile or DeviceType.Tablet))
                {
                    errors.Add($"Android must have DeviceType.Mobile or DeviceType.Tablet, but had {info.DeviceType}.");
                }
                if (string.IsNullOrWhiteSpace(info.Device))
                {
                    errors.Add("Android entries must specify a device model or identifier.");
                }
                break;

            case OperatingSystemType.IOS:
                if (info.DeviceType is not (DeviceType.Mobile or DeviceType.Tablet))
                {
                    errors.Add($"IOS must have DeviceType.Mobile or DeviceType.Tablet, but had {info.DeviceType}.");
                }
                if (info.DeviceType == DeviceType.Mobile && (info.Device is null || !info.Device.Contains("iPhone", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"IOS Mobile entry device must specify iPhone, but had '{info.Device}'.");
                }
                if (info.DeviceType == DeviceType.Tablet && (info.Device is null || !info.Device.Contains("iPad", StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"IOS Tablet entry device must specify iPad, but had '{info.Device}'.");
                }
                break;
        }

        // 4. Browser vs OS compatibility
        if (info.Browser == Browser.Safari && info.Os is not (OperatingSystemType.MacOS or OperatingSystemType.IOS))
        {
            errors.Add($"Safari is only valid on MacOS or IOS, but was assigned {info.Os}.");
        }

        if (string.IsNullOrWhiteSpace(info.UserAgent))
        {
            return ValidationResult.Failure(errors);
        }

        string ua = info.UserAgent;

        // 5. Operating System token validation in UA string
        switch (info.Os)
        {
            case OperatingSystemType.Windows:
                if (!ua.Contains("Windows NT", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Windows user agent string must contain 'Windows NT'.");
                }
                break;

            case OperatingSystemType.MacOS:
                if (!ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) &&
                    !ua.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("macOS user agent string must contain 'Macintosh' or 'Mac OS X'.");
                }
                break;

            case OperatingSystemType.Linux:
                if (!ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) &&
                    !ua.Contains("X11", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Linux user agent string must contain 'Linux' or 'X11'.");
                }
                break;

            case OperatingSystemType.ChromeOS:
                if (!ua.Contains("CrOS", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("ChromeOS user agent string must contain 'CrOS'.");
                }
                break;

            case OperatingSystemType.Android:
                if (!ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Android user agent string must contain 'Android'.");
                }
                break;

            case OperatingSystemType.IOS:
                if (info.DeviceType == DeviceType.Mobile && !ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("iOS Mobile user agent string must contain 'iPhone'.");
                }
                if (info.DeviceType == DeviceType.Tablet && !ua.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("iOS Tablet user agent string must contain 'iPad'.");
                }
                break;
        }

        // 6. Browser token validation in UA string
        switch (info.Browser)
        {
            case Browser.Chrome:
                if (info.Os == OperatingSystemType.IOS)
                {
                    if (!ua.Contains("CriOS/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("iOS Chrome user agent string must contain 'CriOS/'.");
                    }
                }
                else
                {
                    if (!ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Chrome user agent string must contain 'Chrome/'.");
                    }
                }
                break;

            case Browser.Firefox:
                if (info.Os == OperatingSystemType.IOS)
                {
                    if (!ua.Contains("FxiOS/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("iOS Firefox user agent string must contain 'FxiOS/'.");
                    }
                }
                else
                {
                    if (!ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Firefox user agent string must contain 'Firefox/'.");
                    }
                }
                break;

            case Browser.Edge:
                if (info.Os == OperatingSystemType.IOS)
                {
                    if (!ua.Contains("EdgiOS/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("iOS Edge user agent string must contain 'EdgiOS/'.");
                    }
                }
                else if (info.Os == OperatingSystemType.Android)
                {
                    if (!ua.Contains("EdgA/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Android Edge user agent string must contain 'EdgA/'.");
                    }
                }
                else
                {
                    if (!ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Desktop Edge user agent string must contain 'Edg/'.");
                    }
                }
                break;

            case Browser.Opera:
                if (info.Os == OperatingSystemType.IOS)
                {
                    if (!ua.Contains("OPT/", StringComparison.OrdinalIgnoreCase) &&
                        !ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("iOS Opera user agent string must contain 'OPT/' or 'OPR/'.");
                    }
                }
                else
                {
                    if (!ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add("Opera user agent string must contain 'OPR/'.");
                    }
                }
                break;

            case Browser.Safari:
                if (!ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) ||
                    !ua.Contains("Version/", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Safari user agent string must contain 'Safari' and 'Version/'.");
                }

                // Pure Safari on MacOS/iOS must not contain competitor product tags
                if (ua.Contains("CriOS/", StringComparison.OrdinalIgnoreCase) ||
                    ua.Contains("FxiOS/", StringComparison.OrdinalIgnoreCase) ||
                    ua.Contains("EdgiOS/", StringComparison.OrdinalIgnoreCase) ||
                    ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ||
                    ua.Contains("EdgA/", StringComparison.OrdinalIgnoreCase) ||
                    ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ||
                    ua.Contains("OPT/", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Safari user agent cannot contain third-party browser identifiers.");
                }
                break;
        }

        return errors.Count == 0 ? ValidationResult.Success : ValidationResult.Failure(errors);
    }

    /// <summary>
    /// Validates an entire collection of user agent entries.
    /// </summary>
    /// <param name="entries">The entries to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> summarizing any validation failures across the dataset.</returns>
    public static ValidationResult ValidateAll(IEnumerable<UserAgentInfo>? entries)
    {
        if (entries is null)
        {
            return ValidationResult.Failure("Entries collection cannot be null.");
        }

        var allErrors = new List<string>();
        int index = 0;

        foreach (var entry in entries)
        {
            var result = Validate(entry);
            if (!result.IsValid)
            {
                foreach (var err in result.Errors)
                {
                    allErrors.Add($"Entry #{index} ({entry?.Browser}/{entry?.Os}): {err}");
                }
            }
            index++;
        }

        return allErrors.Count == 0 ? ValidationResult.Success : ValidationResult.Failure(allErrors);
    }
}
