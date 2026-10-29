using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class UserAgentValidatorTests
{
    [Fact]
    public void Validate_NullEntry_ReturnsFailure()
    {
        var result = UserAgentValidator.Validate(null);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("null"));
    }

    [Fact]
    public void Validate_ValidEntry_ReturnsSuccess()
    {
        var valid = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            Browser = Browser.Chrome,
            BrowserVersion = "131.0.0.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11",
            Device = null
        };

        var result = UserAgentValidator.Validate(valid);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_SafariOnWindows_ReturnsFailure()
    {
        var invalid = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/605.1.15 Version/18.0 Safari/605.1.15",
            Browser = Browser.Safari,
            BrowserVersion = "18.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10",
            Device = null
        };

        var result = UserAgentValidator.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Safari is only valid on MacOS or IOS"));
    }

    [Fact]
    public void Validate_AndroidOnDesktop_ReturnsFailure()
    {
        var invalid = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Linux; Android 14; Pixel 8) Chrome/130.0",
            Browser = Browser.Chrome,
            BrowserVersion = "130.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Android,
            OsVersion = "14",
            Device = "Pixel 8"
        };

        var result = UserAgentValidator.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Android must have DeviceType.Mobile or DeviceType.Tablet"));
    }

    [Fact]
    public void Validate_MismatchedUserAgentTokens_ReturnsFailure()
    {
        var mismatched = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0",
            Browser = Browser.Chrome, // Claims Chrome, but string is Firefox
            BrowserVersion = "131.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11",
            Device = null
        };

        var result = UserAgentValidator.Validate(mismatched);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Chrome user agent string must contain 'Chrome/'"));
    }
}
