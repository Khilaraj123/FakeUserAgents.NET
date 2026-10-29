using Dotnet.FakeUserAgents.Internal;
using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class UaDatabaseTests
{
    [Fact]
    public void Database_LoadsSuccessfully_AndHasValidEntries()
    {
        var entries = UaDatabase.Entries;

        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.UserAgent));
            Assert.False(string.IsNullOrWhiteSpace(e.BrowserVersion));
            Assert.False(string.IsNullOrWhiteSpace(e.OsVersion));
            Assert.True(e.Weight > 0);
        });
    }

    [Fact]
    public void AllDatabaseEntries_PassStrongValidation()
    {
        var result = UserAgentValidator.ValidateAll(UaDatabase.Entries);

        Assert.True(result.IsValid, $"Dataset validation failed: {result.Summary}");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void AllFallbackEntries_PassStrongValidation()
    {
        var result = UserAgentValidator.ValidateAll(UaDatabase.FallbackEntries);

        Assert.True(result.IsValid, $"Fallback validation failed: {result.Summary}");
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Database_HasComprehensiveBrowserCoverage()
    {
        var entries = UaDatabase.Entries;

        foreach (Browser browser in Enum.GetValues<Browser>())
        {
            Assert.Contains(entries, e => e.Browser == browser);
        }
    }

    [Fact]
    public void Database_HasComprehensiveDeviceCoverage()
    {
        var entries = UaDatabase.Entries;

        foreach (DeviceType device in Enum.GetValues<DeviceType>())
        {
            Assert.Contains(entries, e => e.DeviceType == device);
        }
    }

    [Fact]
    public void Database_HasComprehensiveOperatingSystemCoverage()
    {
        var entries = UaDatabase.Entries;

        foreach (OperatingSystemType os in Enum.GetValues<OperatingSystemType>())
        {
            Assert.Contains(entries, e => e.Os == os);
        }
    }

    [Fact]
    public void Dataset_NewestChromeVersion_IsNotStale()
    {
        // CI Recency Check: fails if the newest Chrome version in the dataset is older than major 144
        int maxChromeMajor = UaDatabase.Entries
            .Where(e => e.Browser == Browser.Chrome)
            .Select(e => int.TryParse(e.BrowserVersion.Split('.')[0], out int major) ? major : 0)
            .Max();

        Assert.True(maxChromeMajor >= 144,
            $"Newest Chrome version ({maxChromeMajor}) is too stale. Expected at least Chrome 144.");
    }

    [Fact]
    public void Dataset_Weights_ReflectMarketShareDistribution()
    {
        var entries = UaDatabase.Entries;
        double totalWeight = entries.Sum(e => e.Weight);
        double chromeWeight = entries.Where(e => e.Browser == Browser.Chrome).Sum(e => e.Weight);
        double safariWeight = entries.Where(e => e.Browser == Browser.Safari).Sum(e => e.Weight);

        double chromeShare = (chromeWeight / totalWeight) * 100.0;
        double safariShare = (safariWeight / totalWeight) * 100.0;

        // In real world traffic, Chrome is ~60-70% and Safari is ~15-25%
        Assert.True(chromeShare >= 55.0, $"Chrome market share weight ({chromeShare:F1}%) is too low. Expected >= 55%.");
        Assert.True(safariShare >= 15.0, $"Safari market share weight ({safariShare:F1}%) is too low. Expected >= 15%.");
    }

    [Fact]
    public void Dataset_SupportedCombinations_HavePoolDepthForRotation()
    {
        // Ensure no thin single-entry pools in key browser/device/OS combinations
        var testCombinations = new (Browser b, DeviceType d, OperatingSystemType o)[]
        {
            (Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Windows),
            (Browser.Chrome, DeviceType.Desktop, OperatingSystemType.MacOS),
            (Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Linux),
            (Browser.Chrome, DeviceType.Mobile, OperatingSystemType.Android),
            (Browser.Chrome, DeviceType.Tablet, OperatingSystemType.IOS),
            (Browser.Edge, DeviceType.Desktop, OperatingSystemType.Linux),
            (Browser.Edge, DeviceType.Mobile, OperatingSystemType.Android),
            (Browser.Firefox, DeviceType.Desktop, OperatingSystemType.Windows),
            (Browser.Safari, DeviceType.Desktop, OperatingSystemType.MacOS),
            (Browser.Safari, DeviceType.Mobile, OperatingSystemType.IOS),
            (Browser.Safari, DeviceType.Tablet, OperatingSystemType.IOS),
            (Browser.Opera, DeviceType.Desktop, OperatingSystemType.Linux),
            (Browser.Opera, DeviceType.Desktop, OperatingSystemType.MacOS),
            (Browser.Opera, DeviceType.Mobile, OperatingSystemType.Android)
        };

        foreach (var (b, d, o) in testCombinations)
        {
            bool found = UaIndex.TryGetPool(b, d, o, out var pool);
            Assert.True(found, $"Missing pool for {b} on {d} {o}");
            Assert.True(pool.Length >= 3, $"Pool for {b} on {d} {o} has only {pool.Length} entries. Expected at least 3 for effective rotation.");
        }
    }
}
