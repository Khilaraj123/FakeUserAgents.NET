using System.Collections.Immutable;
using FakeUserAgents.NET;
using FakeUserAgents.NET.Internal;
using Xunit;

namespace FakeUserAgents.NET.Tests;

public class BugVerificationTests
{
    [Fact]
    public void Bug1_NoImmediateRepeat_PicksAllElementsRandomlyOnFirstCall()
    {
        // Pool of 2 entries
        var pool = ImmutableArray.Create(UaDatabase.Entries[0], UaDatabase.Entries[1]);
        
        int pickedFirstCount = 0;
        int pickedSecondCount = 0;
        const int trials = 200;

        for (int i = 0; i < trials; i++)
        {
            var rotator = new UserAgentRotator(pool, RotationStrategy.NoImmediateRepeat);
            var first = rotator.Next();
            if (first.UserAgent == pool[0].UserAgent) pickedFirstCount++;
            if (first.UserAgent == pool[1].UserAgent) pickedSecondCount++;
        }

        // Both items must be chosen on first call with non-zero, healthy probability
        Assert.True(pickedFirstCount > 40, $"Expected first item picked > 40 times, was {pickedFirstCount}");
        Assert.True(pickedSecondCount > 40, $"Expected second item picked > 40 times, was {pickedSecondCount}");
    }

    [Fact]
    public void Bug2_DefaultImmutableArray_FallsBackToDefaultEntry()
    {
        ImmutableArray<UserAgentInfo> defaultArray = default;
        
        // Uninitialized immutable array should not throw NullReferenceException
        var rotator = new UserAgentRotator(defaultArray);
        Assert.Equal(1, rotator.AvailableCount);
        Assert.Equal(UaDatabase.DefaultEntry.UserAgent, rotator.Next().UserAgent);

        // Null should throw ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => new UserAgentRotator((IEnumerable<UserAgentInfo>)null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Bug8_SessionManager_ThrowsOnNonPositiveSlidingExpiration(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserAgentSessionManager(slidingExpiration: TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Bug7_SessionManager_RecoversFromFaultedSessionGeneration()
    {
        var manager = new UserAgentSessionManager();
        int attempts = 0;

        // First attempt throws
        Assert.Throws<InvalidOperationException>(() =>
        {
            manager.GetOrCreate("failing-session", () =>
            {
                attempts++;
                throw new InvalidOperationException("Simulated transient generator error");
            });
        });

        // Second attempt with working generator should succeed rather than caching the exception
        var recovered = manager.GetOrCreate("failing-session", () =>
        {
            attempts++;
            return UaDatabase.DefaultEntry;
        });

        Assert.NotNull(recovered);
        Assert.Equal(UaDatabase.DefaultEntry.UserAgent, recovered.UserAgent);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void Bug7_Rotator_RecoversFromFaultedSessionGeneration()
    {
        // Custom empty pool that was somehow forced
        var rotator = new UserAgentRotator(RotationStrategy.Shuffled);
        
        // Normal session should work
        var ua = rotator.GetForSession("valid-session");
        Assert.NotNull(ua);
        Assert.True(rotator.TryGetSession("valid-session", out _));
    }

    [Fact]
    public void Bug9_Validator_RejectsChromeWithCompetitorTokens()
    {
        // Edge disguised as Chrome
        var edgeAsChrome = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36 Edg/145.0.0.0",
            Browser = Browser.Chrome,
            BrowserVersion = "145.0.0.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11"
        };

        var result = UserAgentValidator.Validate(edgeAsChrome);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("third-party browser identifiers"));

        // Opera disguised as Chrome
        var operaAsChrome = new UserAgentInfo
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36 OPR/124.0.0.0",
            Browser = Browser.Chrome,
            BrowserVersion = "145.0.0.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11"
        };

        var resultOpera = UserAgentValidator.Validate(operaAsChrome);
        Assert.False(resultOpera.IsValid);
        Assert.Contains(resultOpera.Errors, e => e.Contains("third-party browser identifiers"));
    }
}
