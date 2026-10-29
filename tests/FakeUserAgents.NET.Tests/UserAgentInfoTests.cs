using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class UserAgentInfoTests
{
    [Fact]
    public void Properties_AreStoredAndRetrievable()
    {
        var info = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            Browser = Browser.Chrome,
            BrowserVersion = "131.0.0.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11",
            Device = null
        };

        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", info.UserAgent);
        Assert.Equal(Browser.Chrome, info.Browser);
        Assert.Equal("131.0.0.0", info.BrowserVersion);
        Assert.Equal(DeviceType.Desktop, info.DeviceType);
        Assert.Equal(OperatingSystemType.Windows, info.Os);
        Assert.Equal("10/11", info.OsVersion);
        Assert.Null(info.Device);
    }

    [Fact]
    public void ToString_ReturnsRawUserAgentString()
    {
        var uaString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        var info = new UserAgentInfo
        {
            UserAgent = uaString,
            Browser = Browser.Chrome,
            BrowserVersion = "131.0.0.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11"
        };

        Assert.Equal(uaString, info.ToString());
    }

    [Fact]
    public void ToDictionary_ContainsAllExpectedKeysAndValues()
    {
        var info = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36",
            Browser = Browser.Chrome,
            BrowserVersion = "130.0",
            DeviceType = DeviceType.Mobile,
            Os = OperatingSystemType.Android,
            OsVersion = "14",
            Device = "Pixel 8"
        };

        var dict = info.ToDictionary();

        Assert.Equal(info.UserAgent, dict["userAgent"]);
        Assert.Equal("Chrome", dict["browser"]);
        Assert.Equal("130.0", dict["browserVersion"]);
        Assert.Equal("Mobile", dict["deviceType"]);
        Assert.Equal("Android", dict["os"]);
        Assert.Equal("14", dict["osVersion"]);
        Assert.Equal("Pixel 8", dict["device"]);
    }
}
