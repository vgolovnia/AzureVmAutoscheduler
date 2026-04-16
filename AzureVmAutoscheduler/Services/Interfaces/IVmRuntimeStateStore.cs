using AzureVmAutoscheduler.Models;

namespace AzureVmAutoscheduler.Services.Interfaces;

public interface IVmRuntimeStateStore
{
    Task<IReadOnlyCollection<VmRuntimeStateEntry>> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(IReadOnlyCollection<VmRuntimeStateEntry> entries, CancellationToken cancellationToken);
}
