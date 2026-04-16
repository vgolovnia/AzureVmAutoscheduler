using System.Text;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Services;

public sealed class CsvLogService : ICsvLogService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CsvLogService(IOptions<CsvOptions> options)
    {
        _filePath = options.Value.FilePath;
    }

    public async Task AppendRowsAsync(IEnumerable<VmLogRow> rows, CancellationToken cancellationToken)
    {
        var materialized = rows.ToList();
        if (materialized.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var fileExists = File.Exists(_filePath);

            await using var stream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);

            await using var writer = new StreamWriter(stream, Encoding.UTF8);

            if (!fileExists)
            {
                await writer.WriteLineAsync("TimestampUtc,SubscriptionId,ResourceGroup,ComputerName,PowerState");
            }

            foreach (var row in materialized)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = string.Join(",",
                    Escape(row.TimestampUtc.ToString("O")),
                    Escape(row.SubscriptionId),
                    Escape(row.ResourceGroup),
                    Escape(row.ComputerName),
                    Escape(row.PowerState));

                await writer.WriteLineAsync(line);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
