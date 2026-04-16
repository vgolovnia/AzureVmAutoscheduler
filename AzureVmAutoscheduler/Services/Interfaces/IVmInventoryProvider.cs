using AzureVmAutoscheduler.Models;

namespace AzureVmAutoscheduler.Services.Interfaces;

public interface IVmInventoryProvider
{
    Task<IReadOnlyCollection<VmInfo>> GetAllVmsAsync(CancellationToken cancellationToken);
}
