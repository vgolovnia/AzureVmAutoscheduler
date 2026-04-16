using AzureVmAutoscheduler.Models;

namespace AzureVmAutoscheduler.Services.Interfaces;

public interface ICsvLogService
{
    Task AppendRowsAsync(IEnumerable<VmLogRow> rows, CancellationToken cancellationToken);
}
