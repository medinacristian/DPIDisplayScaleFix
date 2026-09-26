using DPIDisplayScaleFix.Core;

public sealed class StableTopologyTrackerTests
{
    private static readonly TimeSpan StablePeriod = TimeSpan.FromSeconds(5);

    [Fact]
    public void Commits_a_change_only_after_it_stays_stable_for_the_full_period()
    {
        var clock = new FakeTimeProvider();
        var tracker = new StableTopologyTracker("internal", StablePeriod, clock);

        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(tracker.Observe("internal+external"));
        Assert.False(tracker.Observe("internal+external"));
    }

    [Fact]
    public void A_new_change_restarts_the_stability_period()
    {
        var clock = new FakeTimeProvider();
        var tracker = new StableTopologyTracker("internal", StablePeriod, clock);

        Assert.False(tracker.Observe("internal+LG1"));
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(tracker.Observe("internal+LG2"));
        clock.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(tracker.Observe("internal+LG2"));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(tracker.Observe("internal+LG2"));
    }

    [Fact]
    public void Returning_to_the_known_topology_cancels_a_pending_change()
    {
        var clock = new FakeTimeProvider();
        var tracker = new StableTopologyTracker("internal", StablePeriod, clock);

        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(tracker.Observe("internal"));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.True(tracker.Observe("internal+external"));
    }

    [Fact]
    public void A_transient_query_failure_can_reset_the_pending_period()
    {
        var clock = new FakeTimeProvider();
        var tracker = new StableTopologyTracker("internal", StablePeriod, clock);

        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromSeconds(5));
        tracker.ResetCandidate();
        Assert.False(tracker.Observe("internal+external"));
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(tracker.Observe("internal+external"));
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
}