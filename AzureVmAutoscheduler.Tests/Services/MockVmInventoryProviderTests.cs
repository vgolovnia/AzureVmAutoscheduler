using System.Text.Json;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Tests.Services;

[TestFixture]
public class MockVmInventoryProviderTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AzureVmAutoschedulerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public async Task GetAllVmsAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var provider = new MockVmInventoryProvider(
            Microsoft.Extensions.Options.Options.Create(new AppOptions { MockDataFile = Path.Combine(_tempDir, "missing.json") }),
            NullLogger<MockVmInventoryProvider>.Instance);

        var result = await provider.GetAllVmsAsync(CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetAllVmsAsync_LoadsItemsFromJson()
    {
        var filePath = Path.Combine(_tempDir, "mock-vms.json");
        var sample = new[]
        {
            new VmInfo
            {
                SubscriptionId = "sub-001",
                ResourceGroup = "rg-app",
                Name = "vm1",
                PowerState = "running",
                IsAllocated = true,
                Tags = new Dictionary<string, string> { ["Autoshutdown"] = "1" }
            }
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(sample));

        var provider = new MockVmInventoryProvider(
            Microsoft.Extensions.Options.Options.Create(new AppOptions { MockDataFile = filePath }),
            NullLogger<MockVmInventoryProvider>.Instance);

        var result = await provider.GetAllVmsAsync(CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Name, Is.EqualTo("vm1"));
        Assert.That(result.First().HasAutoshutdownTag, Is.True);
    }
}
