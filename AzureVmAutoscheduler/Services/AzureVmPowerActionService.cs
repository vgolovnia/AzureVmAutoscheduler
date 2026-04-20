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
        if (action == VmActionType.None)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var resourceName = string.IsNullOrWhiteSpace(vm.ResourceName) ? vm.Name : vm.ResourceName;
        var vmResourceId = VirtualMachineResource.CreateResourceIdentifier(vm.SubscriptionId, vm.ResourceGroup, resourceName);
        var vmResource = _armClient.GetVirtualMachineResource(vmResourceId);

        _logger.LogInformation(
            "Executing {Action} for VM {SubscriptionId}/{ResourceGroup}/{VmName}",
            action,
            vm.SubscriptionId,
            vm.ResourceGroup,
            resourceName);

        switch (action)
        {
            case VmActionType.Shutdown:
                await vmResource.PowerOffAsync(WaitUntil.Completed, skipShutdown: false, cancellationToken);
                _logger.LogInformation(
                    "Completed {Action} for VM {SubscriptionId}/{ResourceGroup}/{VmName}",
                    action,
                    vm.SubscriptionId,
                    vm.ResourceGroup,
                    resourceName);
                return;

            case VmActionType.Deallocate:
                await vmResource.DeallocateAsync(WaitUntil.Completed, cancellationToken: cancellationToken);
                _logger.LogInformation(
                    "Completed {Action} for VM {SubscriptionId}/{ResourceGroup}/{VmName}",
                    action,
                    vm.SubscriptionId,
                    vm.ResourceGroup,
                    resourceName);
                return;

            default:
                _logger.LogWarning(
                    "Unsupported action {Action} requested for VM {SubscriptionId}/{ResourceGroup}/{VmName}",
                    action,
                    vm.SubscriptionId,
                    vm.ResourceGroup,
                    resourceName);
                return;
        }
    }
}
