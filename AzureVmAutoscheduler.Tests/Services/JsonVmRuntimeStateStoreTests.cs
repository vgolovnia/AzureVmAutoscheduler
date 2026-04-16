using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AzureVmAutoscheduler.Tests.Services;

[TestFixture]
public class JsonVmRuntimeStateStoreTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"AzureVmAutoschedulerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsEntries()
    {
        var filePath = Path.Combine(_tempDirectory, "state.json");
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions { RuntimeStateFile = filePath });
        var store = new JsonVmRuntimeStateStore(options, NullLogger<JsonVmRuntimeStateStore>.Instance);
        var now = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc);

        await store.SaveAsync(
        [
            new VmRuntimeStateEntry { VmKey = "sub/rg/vm1", FirstSeenRunningUtc = now }
        ], CancellationToken.None);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded.Single().VmKey, Is.EqualTo("sub/rg/vm1"));
        Assert.That(loaded.Single().FirstSeenRunningUtc, Is.EqualTo(now));
    }

    [Test]
    public async Task LoadAsync_WhenFileMissing_ReturnsEmptyCollection()
    {
        var filePath = Path.Combine(_tempDirectory, "missing.json");
        var options = Microsoft.Extensions.Options.Options.Create(new AppOptions { RuntimeStateFile = filePath });
        var store = new JsonVmRuntimeStateStore(options, NullLogger<JsonVmRuntimeStateStore>.Instance);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.That(loaded, Is.Empty);
    }
}
