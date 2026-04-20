using System.Globalization;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services.Interfaces;
using CsvHelper;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Services;

public sealed class CsvLogService : ICsvLogService, IDisposable
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public CsvLogService(IOptions<CsvOptions> options)
    {
        _filePath = options.Value.FilePath;
    }

    public async Task AppendRowsAsync(IEnumerable<VmLogRow> rows, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var materialized = rows.ToList();
        if (materialized.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileExists = File.Exists(_filePath);

            await using var stream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                useAsync: true);

            await using var writer = new StreamWriter(stream);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            if (!fileExists)
            {
                csv.WriteHeader<VmLogRow>();
                await csv.NextRecordAsync();
            }

            foreach (var row in materialized)
            {
                cancellationToken.ThrowIfCancellationRequested();
                csv.WriteRecord(row);
                await csv.NextRecordAsync();
            }

            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        _disposed = true;
    }
}
