using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class UserAgentTests
{
    [Fact]
    public void GetRandom_ReturnsValidUserAgent()
    {
        var ua = new UserAgent();
        var result = ua.GetRandom();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.UserAgent));
        var validation = UserAgentValidator.Validate(result);
        Assert.True(validation.IsValid, validation.Summary);
    }

    [Fact]
    public void GetRandom_Weighted_ReflectsMarketShareOverManySamples()
    {
        var ua = new UserAgent(seed: 42);
        int chromeCount = 0;
        const int samples = 5_000;

        for (int i = 0; i < samples; i++)
        {
            var item = ua.GetRandom(weighted: true);
            if (item.Browser == Browser.Chrome)
            {
                chromeCount++;
            }
        }

        double chromePercentage = (double)chromeCount / samples * 100.0;
        // Chrome should represent the vast majority of weighted samples (> 50%)
        Assert.True(chromePercentage >= 50.0, $"Expected Chrome to dominate weighted samples, but was {chromePercentage:F1}%");
    }

    [Fact]
    public void GetRandom_Uniform_AllowsAllBrowsersToBeSampled()
    {
        var ua = new UserAgent(seed: 42);
        var seenBrowsers = new HashSet<Browser>();

        for (int i = 0; i < 500; i++)
        {
            var item = ua.GetRandom(weighted: false);
            seenBrowsers.Add(item.Browser);
        }

        Assert.Equal(Enum.GetValues<Browser>().Length, seenBrowsers.Count);
    }

    [Theory]
    [InlineData(Browser.Chrome)]
    [InlineData(Browser.Firefox)]
    [InlineData(Browser.Safari)]
    [InlineData(Browser.Edge)]
    [InlineData(Browser.Opera)]
    public void GetByBrowser_Enum_ReturnsRequestedBrowser(Browser browser)
    {
        var ua = new UserAgent();
        var result = ua.GetByBrowser(browser);

        Assert.Equal(browser, result.Browser);
    }

    [Theory]
    [InlineData("chrome", Browser.Chrome)]
    [InlineData("FIREFOX", Browser.Firefox)]
    [InlineData("Safari", Browser.Safari)]
    [InlineData("edge", Browser.Edge)]
    [InlineData("Opera", Browser.Opera)]
    public void GetByBrowser_String_IsCaseInsensitive(string browserName, Browser expected)
    {
        var ua = new UserAgent();
        var result = ua.GetByBrowser(browserName);

        Assert.Equal(expected, result.Browser);
    }

    [Fact]
    public void GetByBrowser_UnknownString_FallsBackToRandom()
    {
        var ua = new UserAgent();
        var result = ua.GetByBrowser("NonExistentBrowser1234");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.UserAgent));
    }

    [Theory]
    [InlineData(DeviceType.Desktop)]
    [InlineData(DeviceType.Mobile)]
    [InlineData(DeviceType.Tablet)]
    public void GetByDevice_ReturnsRequestedDevice(DeviceType device)
    {
        var ua = new UserAgent();
        var result = ua.GetByDevice(device);

        Assert.Equal(device, result.DeviceType);
    }

    [Theory]
    [InlineData(OperatingSystemType.Windows)]
    [InlineData(OperatingSystemType.MacOS)]
    [InlineData(OperatingSystemType.Linux)]
    [InlineData(OperatingSystemType.Android)]
    [InlineData(OperatingSystemType.IOS)]
    public void GetByOs_ReturnsRequestedOperatingSystem(OperatingSystemType os)
    {
        var ua = new UserAgent();
        var result = ua.GetByOs(os);

        Assert.Equal(os, result.Os);
    }

    [Fact]
    public void Get_FallbackChain_DegradesGracefullyForNonExistentCombination()
    {
        var ua = new UserAgent();
        // Safari does not exist on Windows
        var result = ua.Get(Browser.Safari, DeviceType.Desktop, OperatingSystemType.Windows);

        Assert.NotNull(result);
        // Fallback chain relaxes OS first, giving Safari Desktop (MacOS)
        Assert.Equal(Browser.Safari, result.Browser);
        Assert.Equal(DeviceType.Desktop, result.DeviceType);
    }

    [Fact]
    public void TryGet_ReturnsTrue_ForExistingCombination()
    {
        var ua = new UserAgent();
        bool found = ua.TryGet(Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Windows, out var result);

        Assert.True(found);
        Assert.NotNull(result);
        Assert.Equal(Browser.Chrome, result.Browser);
        Assert.Equal(OperatingSystemType.Windows, result.Os);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForNonExistentCombination()
    {
        var ua = new UserAgent();
        // Safari on Windows does not exist
        bool found = ua.TryGet(Browser.Safari, DeviceType.Desktop, OperatingSystemType.Windows, out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void DeterministicSeeding_ProducesIdenticalSequences()
    {
        var ua1 = new UserAgent(seed: 12345);
        var ua2 = new UserAgent(seed: 12345);

        for (int i = 0; i < 20; i++)
        {
            var item1 = ua1.GetRandom();
            var item2 = ua2.GetRandom();

            Assert.Equal(item1.UserAgent, item2.UserAgent);
            Assert.Equal(item1.Browser, item2.Browser);
            Assert.Equal(item1.Os, item2.Os);
        }
    }

    [Fact]
    public void GetRandomAsDictionary_ReturnsPopulatedDictionary()
    {
        var ua = new UserAgent();
        var dict = ua.GetRandomAsDictionary();

        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("userAgent"));
        Assert.True(dict.ContainsKey("browser"));
        Assert.True(dict.ContainsKey("weight"));
    }
}
