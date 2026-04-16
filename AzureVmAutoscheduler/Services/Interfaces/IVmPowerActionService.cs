using AzureVmAutoscheduler.Models;

namespace AzureVmAutoscheduler.Services.Interfaces;

public interface IVmPowerActionService
{
    Task ExecuteAsync(VmInfo vm, VmActionType action, CancellationToken cancellationToken);
}
