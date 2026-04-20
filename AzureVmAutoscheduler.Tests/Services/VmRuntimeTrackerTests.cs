using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Services;
using AzureVmAutoscheduler.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzureVmAutoscheduler.Tests.Services;

[TestFixture]
public class VmRuntimeTrackerTests
{
    [Test]
    public async Task InitializeAsync_LoadsPersistedEntries()
    {
        var now = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);
        var store = new InMemoryVmRuntimeStateStore(
        [
            new VmRuntimeStateEntry { VmKey = "sub/rg/vm1", FirstSeenRunningUtc = now }
        ]);

        var tracker = new VmRuntimeTracker(store, NullLogger<VmRuntimeTracker>.Instance);

        await tracker.InitializeAsync(CancellationToken.None);

        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm1"), Is.EqualTo(now));
    }

    [Test]
    public void UpdateState_SetsAndClearsRunningTimestamp()
    {
        var tracker = new VmRuntimeTracker(new InMemoryVmRuntimeStateStore(), NullLogger<VmRuntimeTracker>.Instance);
        var firstSeen = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);

        tracker.UpdateState("sub/rg/vm1", true, firstSeen, firstSeen);
        var runningSince = tracker.GetRunningSinceUtc("sub/rg/vm1");

        Assert.That(runningSince, Is.EqualTo(firstSeen));

        tracker.UpdateState("sub/rg/vm1", false, firstSeen.AddHours(1), null);

        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm1"), Is.Null);
    }

    [Test]
    public void RemoveMissing_RemovesEntriesThatAreNoLongerPresent()
    {
        var tracker = new VmRuntimeTracker(new InMemoryVmRuntimeStateStore(), NullLogger<VmRuntimeTracker>.Instance);
        var now = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);

        tracker.UpdateState("sub/rg/vm1", true, now, now);
        tracker.UpdateState("sub/rg/vm2", true, now, now);

        tracker.RemoveMissing(["sub/rg/vm2"]);

        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm1"), Is.Null);
        Assert.That(tracker.GetRunningSinceUtc("sub/rg/vm2"), Is.EqualTo(now));
    }

    [Test]
    public async Task PersistAsync_SavesSnapshotToStore()
    {
        var store = new InMemoryVmRuntimeStateStore();
        var tracker = new VmRuntimeTracker(store, NullLogger<VmRuntimeTracker>.Instance);
        var now = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);

        tracker.UpdateState("sub/rg/vm1", true, now, now);
        await tracker.PersistAsync(CancellationToken.None);

        Assert.That(store.SavedEntries, Has.Count.EqualTo(1));
        Assert.That(store.SavedEntries[0].VmKey, Is.EqualTo("sub/rg/vm1"));
        Assert.That(store.SavedEntries[0].FirstSeenRunningUtc, Is.EqualTo(now));
    }

    private sealed class InMemoryVmRuntimeStateStore : IVmRuntimeStateStore
    {
        private readonly IReadOnlyCollection<VmRuntimeStateEntry> _entries;

        public InMemoryVmRuntimeStateStore(IReadOnlyCollection<VmRuntimeStateEntry>? entries = null)
        {
            _entries = entries ?? [];
        }

        public List<VmRuntimeStateEntry> SavedEntries { get; } = [];

        public Task<IReadOnlyCollection<VmRuntimeStateEntry>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_entries);
        }

        public Task SaveAsync(IReadOnlyCollection<VmRuntimeStateEntry> entries, CancellationToken cancellationToken)
        {
            SavedEntries.Clear();
            SavedEntries.AddRange(entries);
            return Task.CompletedTask;
        }
    }
}