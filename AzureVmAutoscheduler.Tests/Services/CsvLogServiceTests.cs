using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services;

namespace AzureVmAutoscheduler.Tests.Services;

[TestFixture]
public class CsvLogServiceTests
{
    private string _tempDir = null!;
    private string _csvPath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AzureVmAutoschedulerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _csvPath = Path.Combine(_tempDir, "vm-log.csv");
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
    public async Task AppendRowsAsync_WritesHeaderAndRows_WhenFileDoesNotExist()
    {
        var service = new CsvLogService(Microsoft.Extensions.Options.Options.Create(new CsvOptions { FilePath = _csvPath }));
        var timestamp = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc);

        await service.AppendRowsAsync(new[]
        {
            new VmLogRow
            {
                TimestampUtc = timestamp,
                SubscriptionId = "sub-001",
                ResourceGroup = "rg-app",
                ComputerName = "vm1",
                PowerState = "running"
            }
        }, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(_csvPath);
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines[0], Is.EqualTo("TimestampUtc,SubscriptionId,ResourceGroup,ComputerName,PowerState"));
        Assert.That(lines[1], Does.Contain("sub-001"));
        Assert.That(lines[1], Does.Contain("vm1"));
    }

    [Test]
    public async Task AppendRowsAsync_AppendsWithoutRepeatingHeader()
    {
        var service = new CsvLogService(Microsoft.Extensions.Options.Options.Create(new CsvOptions { FilePath = _csvPath }));
        var timestamp = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc);

        await service.AppendRowsAsync(new[]
        {
            new VmLogRow { TimestampUtc = timestamp, SubscriptionId = "sub-001", ResourceGroup = "rg1", ComputerName = "vm1", PowerState = "running" }
        }, CancellationToken.None);

        await service.AppendRowsAsync(new[]
        {
            new VmLogRow { TimestampUtc = timestamp, SubscriptionId = "sub-002", ResourceGroup = "rg2", ComputerName = "vm2", PowerState = "stopped" }
        }, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(_csvPath);
        Assert.That(lines, Has.Length.EqualTo(3));
        Assert.That(lines.Count(l => l.StartsWith("TimestampUtc,")), Is.EqualTo(1));
    }

    [Test]
    public async Task AppendRowsAsync_EscapesCommaAndQuotes()
    {
        var service = new CsvLogService(Microsoft.Extensions.Options.Options.Create(new CsvOptions { FilePath = _csvPath }));

        await service.AppendRowsAsync(new[]
        {
            new VmLogRow
            {
                TimestampUtc = DateTime.UtcNow,
                SubscriptionId = "sub,001",
                ResourceGroup = "rg\"quoted\"",
                ComputerName = "vm1",
                PowerState = "running"
            }
        }, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(_csvPath);
        Assert.That(lines[1], Does.Contain("\"sub,001\""));
        Assert.That(lines[1], Does.Contain("\"rg\"\"quoted\"\"\""));
    }
}
