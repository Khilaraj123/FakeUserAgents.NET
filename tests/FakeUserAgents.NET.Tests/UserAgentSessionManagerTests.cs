using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class UserAgentSessionManagerTests
{
    [Fact]
    public void GetOrCreate_PersistsSessionAffinity()
    {
        var manager = new UserAgentSessionManager();

        var ua1 = manager.GetOrCreate("session-101");
        var ua2 = manager.GetOrCreate("session-101");

        Assert.Equal(ua1.UserAgent, ua2.UserAgent);
        Assert.Equal(1, manager.Count);
    }

    [Fact]
    public void TryGet_ReturnsAssignedSessionOrFalse()
    {
        var manager = new UserAgentSessionManager();

        Assert.False(manager.TryGet("non-existent", out var notFound));
        Assert.Null(notFound);

        var created = manager.GetOrCreate("session-202");
        Assert.True(manager.TryGet("session-202", out var found));
        Assert.NotNull(found);
        Assert.Equal(created.UserAgent, found.UserAgent);
    }

    [Fact]
    public void Set_ExplicitlyOverridesSession()
    {
        var manager = new UserAgentSessionManager();
        var custom = new UserAgentInfo
        {
            UserAgent = "Custom-Agent-1.0",
            Browser = Browser.Chrome,
            BrowserVersion = "131.0",
            DeviceType = DeviceType.Desktop,
            Os = OperatingSystemType.Windows,
            OsVersion = "10/11"
        };

        manager.Set("session-override", custom);
        Assert.True(manager.TryGet("session-override", out var retrieved));
        Assert.Equal(custom.UserAgent, retrieved?.UserAgent);
    }

    [Fact]
    public void RemoveAndClear_ManageSessionCount()
    {
        var manager = new UserAgentSessionManager();

        manager.GetOrCreate("s1");
        manager.GetOrCreate("s2");
        Assert.Equal(2, manager.Count);

        Assert.True(manager.Remove("s1"));
        Assert.Equal(1, manager.Count);

        manager.Clear();
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void SlidingExpiration_PurgesInactiveSessions()
    {
        var manager = new UserAgentSessionManager(slidingExpiration: TimeSpan.FromMilliseconds(50));

        manager.GetOrCreate("expiring-session");
        Assert.Equal(1, manager.Count);

        Thread.Sleep(75);

        int purged = manager.PurgeExpired();
        Assert.Equal(1, purged);
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public void BoundedCapacity_TrimsWhenExceedingMaxSessions()
    {
        var manager = new UserAgentSessionManager(maxSessions: 100);

        for (int i = 0; i < 150; i++)
        {
            manager.GetOrCreate($"client-{i}");
        }

        Assert.True(manager.Count <= 120, $"Expected manager count to remain bounded, got {manager.Count}");
    }

    [Fact]
    public void ConcurrentAccess_AtomicCreationExecutesGeneratorOnce()
    {
        var manager = new UserAgentSessionManager();
        int generatorCallCount = 0;

        Parallel.For(0, 50, _ =>
        {
            manager.GetOrCreate("shared-race-key", () =>
            {
                Interlocked.Increment(ref generatorCallCount);
                return new UserAgentInfo
                {
                    UserAgent = "Race-Winner-1.0",
                    Browser = Browser.Chrome,
                    BrowserVersion = "131.0",
                    DeviceType = DeviceType.Desktop,
                    Os = OperatingSystemType.Windows,
                    OsVersion = "10/11"
                };
            });
        });

        Assert.Equal(1, generatorCallCount);
    }
}
