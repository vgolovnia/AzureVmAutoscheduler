using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Resources;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Services;

public sealed class AzureVmInventoryProvider : IVmInventoryProvider
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureVmInventoryProvider> _logger;
    private readonly int _maxParallelism;

    public AzureVmInventoryProvider(
        ArmClient armClient,
        IOptions<AppOptions> options,
        ILogger<AzureVmInventoryProvider> logger)
    {
        _armClient = armClient;
        _logger = logger;
        _maxParallelism = Math.Max(1, options.Value.MaxParallelism);
    }

    public async Task<IReadOnlyCollection<VmInfo>> GetAllVmsAsync(CancellationToken cancellationToken)
    {
        var result = new List<VmInfo>();

        await foreach (SubscriptionResource subscription in _armClient.GetSubscriptions().GetAllAsync(cancellationToken))
        {
            try
            {
                var subscriptionVms = new List<VirtualMachineResource>();

                foreach (VirtualMachineResource vm in subscription.GetVirtualMachines())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    subscriptionVms.Add(vm);
                }

                var vmInfos = await LoadVmInfosAsync(subscription.Data.SubscriptionId, subscriptionVms, cancellationToken);
                result.AddRange(vmInfos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate VMs in subscription {SubscriptionId}", subscription.Data.SubscriptionId);
            }
        }

        return result;
    }

    private async Task<IReadOnlyCollection<VmInfo>> LoadVmInfosAsync(
        string subscriptionId,
        IReadOnlyCollection<VirtualMachineResource> vms,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(_maxParallelism);

        var tasks = vms.Select(async vm =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await BuildVmInfoAsync(subscriptionId, vm, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<VmInfo> BuildVmInfoAsync(
        string subscriptionId,
        VirtualMachineResource vm,
        CancellationToken cancellationToken)
    {
        string powerState = "unknown";
        bool isAllocated = true;

        try
        {
            Response<VirtualMachineInstanceView> instanceViewResponse =
                await vm.InstanceViewAsync(cancellationToken);

            var statuses = instanceViewResponse.Value.Statuses;

            var powerStatus = statuses.FirstOrDefault(s =>
                s.Code != null &&
                s.Code.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase));

            if (powerStatus?.Code is not null)
            {
                powerState = powerStatus.Code["PowerState/".Length..].ToLowerInvariant();
            }

            isAllocated = !string.Equals(powerState, "deallocated", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get instance view for VM {VmName}", vm.Data.Name);
        }

        return new VmInfo
        {
            SubscriptionId = subscriptionId,
            ResourceGroup = vm.Id.ResourceGroupName ?? string.Empty,
            Name = vm.Data.Name ?? string.Empty,
            PowerState = powerState,
            IsAllocated = isAllocated,
            Tags = vm.Data.Tags?.ToDictionary(
                k => k.Key,
                v => v.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
