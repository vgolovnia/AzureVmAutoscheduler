using System.Text.Json;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Services;

public sealed class MockVmInventoryProvider : IVmInventoryProvider
{
    private readonly string _mockDataFile;
    private readonly ILogger<MockVmInventoryProvider> _logger;

    public MockVmInventoryProvider(
        IOptions<AppOptions> options,
        ILogger<MockVmInventoryProvider> logger)
    {
        _mockDataFile = options.Value.MockDataFile;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<VmInfo>> GetAllVmsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_mockDataFile))
        {
            _logger.LogWarning("Mock data file not found: {File}", _mockDataFile);
            return Array.Empty<VmInfo>();
        }

        await using var stream = File.OpenRead(_mockDataFile);
        var items = await JsonSerializer.DeserializeAsync<List<VmInfo>>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        return items?.ToArray() ?? Array.Empty<VmInfo>();
    }
}
