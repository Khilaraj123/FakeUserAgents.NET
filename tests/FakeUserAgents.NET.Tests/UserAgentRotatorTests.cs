using Dotnet.FakeUserAgents.Internal;
using Xunit;

namespace Dotnet.FakeUserAgents.Tests;

public class UserAgentRotatorTests
{
    [Fact]
    public void RoundRobin_RotatesSequentiallyInExactOrder()
    {
        var rotator = new UserAgentRotator(RotationStrategy.RoundRobin);
        var pool = rotator.GetAvailableProfiles();
        int count = rotator.AvailableCount;

        for (int i = 0; i < count; i++)
        {
            var expected = pool[i];
            var actual = rotator.Next();
            Assert.Equal(expected.UserAgent, actual.UserAgent);
        }

        // Wraps around to the first element
        var wrap = rotator.Next();
        Assert.Equal(pool[0].UserAgent, wrap.UserAgent);
    }

    [Fact]
    public void Shuffled_VisitsAllUniqueItemsBeforeRepeating()
    {
        var rotator = new UserAgentRotator(RotationStrategy.Shuffled, seed: 999);
        int count = rotator.AvailableCount;

        var visited = new HashSet<string>();
        for (int i = 0; i < count; i++)
        {
            var next = rotator.Next();
            bool isNew = visited.Add(next.UserAgent);
            Assert.True(isNew, $"Shuffled strategy returned duplicate '{next.UserAgent}' before exhausting all {count} candidates.");
        }

        Assert.Equal(count, visited.Count);

        // Next call should continue without throwing (starts new cycle)
        var afterCycle = rotator.Next();
        Assert.NotNull(afterCycle);
    }

    [Fact]
    public void NoImmediateRepeat_NeverReturnsIdenticalUserAgentConsecutively()
    {
        var rotator = new UserAgentRotator(RotationStrategy.NoImmediateRepeat, seed: 42);

        UserAgentInfo? previous = null;
        for (int i = 0; i < 200; i++)
        {
            var current = rotator.Next();
            if (previous is not null)
            {
                Assert.NotEqual(previous.UserAgent, current.UserAgent);
            }
            previous = current;
        }
    }

    [Fact]
    public void DeterministicSeeding_ProducesIdenticalRotationSequences()
    {
        var rotator1 = new UserAgentRotator(RotationStrategy.Shuffled, seed: 777);
        var rotator2 = new UserAgentRotator(RotationStrategy.Shuffled, seed: 777);

        for (int i = 0; i < 50; i++)
        {
            var u1 = rotator1.Next();
            var u2 = rotator2.Next();

            Assert.Equal(u1.UserAgent, u2.UserAgent);
        }
    }

    [Fact]
    public void GetForSession_ConsistentlyReturnsSameUserAgentForSameSessionId()
    {
        var rotator = new UserAgentRotator(RotationStrategy.Shuffled, seed: 123);

        var sessionAUa1 = rotator.GetForSession("user-session-A");
        var sessionBUa1 = rotator.GetForSession("user-session-B");

        // Next calls for same session IDs must return the exact same user agent
        for (int i = 0; i < 10; i++)
        {
            var sessionAUaNext = rotator.GetForSession("user-session-A");
            Assert.Equal(sessionAUa1.UserAgent, sessionAUaNext.UserAgent);

            var sessionBUaNext = rotator.GetForSession("user-session-B");
            Assert.Equal(sessionBUa1.UserAgent, sessionBUaNext.UserAgent);
        }

        Assert.Equal(2, rotator.ActiveSessionCount);

        // Removal
        Assert.True(rotator.RemoveSession("user-session-A"));
        Assert.Equal(1, rotator.ActiveSessionCount);

        rotator.ClearSessions();
        Assert.Equal(0, rotator.ActiveSessionCount);
    }

    [Fact]
    public void BoundedSessions_TrimsWhenExceedingCapacity()
    {
        var rotator = new UserAgentRotator(RotationStrategy.Shuffled, maxSessions: 100);

        for (int i = 0; i < 150; i++)
        {
            rotator.GetForSession($"session-{i}");
        }

        // Bounded capacity should keep count controlled
        Assert.True(rotator.ActiveSessionCount <= 120, $"Expected bounded count, got {rotator.ActiveSessionCount}");
    }

    [Fact]
    public void FilteredRotator_OnlyReturnsMatchingCandidates()
    {
        var rotator = new UserAgentRotator(
            strategy: RotationStrategy.RoundRobin,
            browser: Browser.Firefox,
            device: DeviceType.Desktop);

        Assert.True(rotator.AvailableCount > 0);

        for (int i = 0; i < rotator.AvailableCount * 2; i++)
        {
            var item = rotator.Next();
            Assert.Equal(Browser.Firefox, item.Browser);
            Assert.Equal(DeviceType.Desktop, item.DeviceType);
        }
    }

    [Fact]
    public void MultithreadedAccess_IsThreadSafe()
    {
        var rotator = new UserAgentRotator(RotationStrategy.Shuffled);

        Parallel.For(0, 1000, i =>
        {
            var ua = rotator.Next();
            Assert.NotNull(ua);

            var sessionUa = rotator.GetForSession($"thread-session-{i % 20}");
            Assert.NotNull(sessionUa);
        });
    }
}
