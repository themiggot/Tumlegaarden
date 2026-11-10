using Tumlegaarden.Web.Services;

namespace Tumlegaarden.Tests;

public class PresenceTrackerTests
{
    /// <summary>Ur der kan stilles frem i testen.</summary>
    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Tirsdag 10. november 2026 kl. 13:30 dansk tid (12:30 UTC).
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ActiveUser_IsOnline_UntilWindowPasses()
    {
        var clock = new Clock(Start);
        var tracker = new PresenceTracker(clock);
        tracker.Touch("1", "anton@tumlegaarden.dk");

        Assert.True(tracker.IsOnline("1"));
        Assert.Equal("Online", tracker.StatusText("1"));

        clock.Now += TimeSpan.FromMinutes(2);
        Assert.True(tracker.IsOnline("1"));

        clock.Now += TimeSpan.FromSeconds(1);
        Assert.False(tracker.IsOnline("1"));
        Assert.Equal("Sidst set 13:30", tracker.StatusText("1"));
    }

    [Fact]
    public void Online_ListsOnlyRecentUsers_SortedByName()
    {
        var clock = new Clock(Start);
        var tracker = new PresenceTracker(clock);
        tracker.Touch("1", "bo@tumlegaarden.dk");
        clock.Now += TimeSpan.FromMinutes(3);
        tracker.Touch("2", "ida@tumlegaarden.dk");
        tracker.Touch("3", "anton@tumlegaarden.dk");

        Assert.Equal(["anton", "ida"], tracker.Online().Select(o => o.DisplayName));
    }

    [Fact]
    public void SignOut_IsOfflineImmediately_ButKeepsLastSeen()
    {
        var tracker = new PresenceTracker(new Clock(Start));
        tracker.Touch("1", "anton@tumlegaarden.dk");
        tracker.SignedOut("1");

        Assert.False(tracker.IsOnline("1"));
        Assert.Empty(tracker.Online());
        Assert.Equal("Sidst set 13:30", tracker.StatusText("1"));

        tracker.Touch("1", "anton@tumlegaarden.dk");   // logger ind igen
        Assert.True(tracker.IsOnline("1"));
    }

    [Fact]
    public void BlockedUser_StaysOffline_UntilUnblocked()
    {
        var tracker = new PresenceTracker(new Clock(Start));
        tracker.Touch("1", "anton@tumlegaarden.dk");
        tracker.Block("1");
        tracker.Touch("1", "anton@tumlegaarden.dk");   // gammel session laver en forespørgsel
        Assert.False(tracker.IsOnline("1"));

        tracker.Unblock("1");
        tracker.Touch("1", "anton@tumlegaarden.dk");
        Assert.True(tracker.IsOnline("1"));
    }

    [Fact]
    public void ForgottenUser_IsGoneAndStaysOffline()
    {
        var tracker = new PresenceTracker(new Clock(Start));
        tracker.Touch("1", "anton@tumlegaarden.dk");
        tracker.Forget("1");
        tracker.Touch("1", "anton@tumlegaarden.dk");

        Assert.False(tracker.IsOnline("1"));
        Assert.Equal("Ikke set siden genstart", tracker.StatusText("1"));
    }

    [Fact]
    public void StatusText_UsesDanishDays()
    {
        var clock = new Clock(Start);
        var tracker = new PresenceTracker(clock);
        tracker.Touch("1", "anton@tumlegaarden.dk");

        clock.Now += TimeSpan.FromDays(1);
        Assert.Equal("Sidst set i går 13:30", tracker.StatusText("1"));

        clock.Now += TimeSpan.FromDays(5);
        Assert.Equal("Sidst set 10. nov.", tracker.StatusText("1"));
    }

    [Fact]
    public void UnknownUser_IsOffline() =>
        Assert.Equal("Ikke set siden genstart", new PresenceTracker(new Clock(Start)).StatusText("findes-ikke"));
}
