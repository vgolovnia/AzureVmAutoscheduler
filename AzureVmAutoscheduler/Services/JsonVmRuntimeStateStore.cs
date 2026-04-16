using System.Text.Json;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Services;

public sealed class JsonVmRuntimeStateStore : IVmRuntimeStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly ILogger<JsonVmRuntimeStateStore> _logger;

    public JsonVmRuntimeStateStore(IOptions<AppOptions> options, ILogger<JsonVmRuntimeStateStore> logger)
    {
        _filePath = Path.GetFullPath(options.Value.RuntimeStateFile);
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<VmRuntimeStateEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var entries = await JsonSerializer.DeserializeAsync<List<VmRuntimeStateEntry>>(stream, JsonOptions, cancellationToken);
        return entries ?? [];
    }

    public async Task SaveAsync(IReadOnlyCollection<VmRuntimeStateEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, entries, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, _filePath, true);
        _logger.LogDebug("Persisted VM runtime state to {FilePath}", _filePath);
    }
}
