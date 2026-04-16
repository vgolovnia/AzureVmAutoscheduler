using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Services.Interfaces;

namespace AzureVmAutoscheduler.Services;

public sealed class MockVmPowerActionService : IVmPowerActionService
{
    private readonly ILogger<MockVmPowerActionService> _logger;

    public MockVmPowerActionService(ILogger<MockVmPowerActionService> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(VmInfo vm, VmActionType action, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MOCK ACTION: {Action} for VM {SubscriptionId}/{ResourceGroup}/{VmName}",
            action,
            vm.SubscriptionId,
            vm.ResourceGroup,
            vm.Name);

        return Task.CompletedTask;
    }
}
