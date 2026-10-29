using Dotnet.FakeUserAgents.Internal;
using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class IndexLookupTests
{
    [Fact]
    public void UaIndex_PrecomputesAllBrowserPools()
    {
        foreach (Browser browser in Enum.GetValues<Browser>())
        {
            var pool = UaIndex.GetByBrowser(browser);
            Assert.NotEmpty(pool);
            Assert.All(pool, e => Assert.Equal(browser, e.Browser));
        }
    }

    [Fact]
    public void UaIndex_PrecomputesAllDevicePools()
    {
        foreach (DeviceType device in Enum.GetValues<DeviceType>())
        {
            var pool = UaIndex.GetByDevice(device);
            Assert.NotEmpty(pool);
            Assert.All(pool, e => Assert.Equal(device, e.DeviceType));
        }
    }

    [Fact]
    public void UaIndex_PrecomputesAllOsPools()
    {
        foreach (OperatingSystemType os in Enum.GetValues<OperatingSystemType>())
        {
            var pool = UaIndex.GetByOs(os);
            Assert.NotEmpty(pool);
            Assert.All(pool, e => Assert.Equal(os, e.Os));
        }
    }

    [Fact]
    public void UaIndex_CombinationLookup_ReturnsDirectPool()
    {
        bool found = UaIndex.TryGetPool(Browser.Chrome, DeviceType.Desktop, OperatingSystemType.Windows, out var pool);

        Assert.True(found);
        Assert.NotEmpty(pool);
        Assert.All(pool, e =>
        {
            Assert.Equal(Browser.Chrome, e.Browser);
            Assert.Equal(DeviceType.Desktop, e.DeviceType);
            Assert.Equal(OperatingSystemType.Windows, e.Os);
        });
    }

    [Fact]
    public void UaIndex_NonExistentCombination_ReturnsFalse()
    {
        // Safari Desktop on Windows does not exist
        bool found = UaIndex.TryGetPool(Browser.Safari, DeviceType.Desktop, OperatingSystemType.Windows, out var pool);

        Assert.False(found);
        Assert.True(pool.IsDefaultOrEmpty);
    }
}
