using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Services.Interfaces;

namespace AzureVmAutoscheduler.Services;

public sealed class AzureVmPowerActionService : IVmPowerActionService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureVmPowerActionService> _logger;

    public AzureVmPowerActionService(ArmClient armClient, ILogger<AzureVmPowerActionService> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    public async Task ExecuteAsync(VmInfo vm, VmActionType action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var vmResourceId = VirtualMachineResource.CreateResourceIdentifier(vm.SubscriptionId, vm.ResourceGroup, vm.Name);
        var vmResource = _armClient.GetVirtualMachineResource(vmResourceId);

        _logger.LogInformation(
            "Executing {Action} for VM {SubscriptionId}/{ResourceGroup}/{VmName}",
            action,
            vm.SubscriptionId,
            vm.ResourceGroup,
            vm.Name);

        switch (action)
        {
            case VmActionType.Shutdown:
                await vmResource.PowerOffAsync(WaitUntil.Completed, skipShutdown: false, cancellationToken);
                break;

            case VmActionType.Deallocate:
                await vmResource.DeallocateAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                break;

            case VmActionType.None:
            default:
                return;
        }
    }
}
