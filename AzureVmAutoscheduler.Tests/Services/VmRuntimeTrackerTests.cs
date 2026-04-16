using AzureVmAutoscheduler.Services;

namespace AzureVmAutoscheduler.Tests.Services;

[TestFixture]
public class VmRuntimeTrackerTests
{
    [Test]
    public void UpdateState_SetsAndClearsRunningTimestamp()
    {
        var tracker = new VmRuntimeTracker();
        var firstSeen = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);

        tracker.UpdateState("sub/rg/vm1", "running", firstSeen);
        var runningSince = tracker.GetRunningSinceUtc("sub/rg/vm1");

        Assert.That(runningSince, Is.EqualTo(firstSeen));

        tracker.UpdateState("sub/rg/vm1", "stopped", firstSeen.AddHours(1));

        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm1"), Is.Null);
    }

    [Test]
    public void RemoveMissing_RemovesEntriesThatAreNoLongerPresent()
    {
        var tracker = new VmRuntimeTracker();
        var now = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);

        tracker.UpdateState("sub/rg/vm1", "running", now);
        tracker.UpdateState("sub/rg/vm2", "running", now);

        tracker.RemoveMissing(["sub/rg/vm2"]);

        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm1"), Is.Null);
        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm2"), Is.EqualTo(now));
    }
}
