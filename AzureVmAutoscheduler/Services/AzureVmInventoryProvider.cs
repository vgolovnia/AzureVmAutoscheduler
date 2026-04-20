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
    private readonly IVmStartTimeProvider _vmStartTimeProvider;
    private readonly ILogger<AzureVmInventoryProvider> _logger;
    private readonly int _maxParallelism;

    public AzureVmInventoryProvider(
        ArmClient armClient,
        IVmStartTimeProvider vmStartTimeProvider,
        IOptions<AppOptions> options,
        ILogger<AzureVmInventoryProvider> logger)
    {
        _armClient = armClient;
        _vmStartTimeProvider = vmStartTimeProvider;
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

                await foreach (var vm in subscription.GetVirtualMachinesAsync(cancellationToken: cancellationToken))
                {
                    subscriptionVms.Add(vm);
                }

                var vmInfos = await LoadVmInfosAsync(
                    subscription.Data.SubscriptionId,
                    subscriptionVms,
                    cancellationToken);

                result.AddRange(vmInfos);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to enumerate VMs in subscription {SubscriptionId}",
                    subscription.Data.SubscriptionId);
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
        var tags = vm.Data.Tags?.ToDictionary(
                k => k.Key,
                v => v.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string powerState = VmPowerStates.Unknown;
        bool isAllocated = true;
        DateTime? startedAtUtc = null;

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

            isAllocated = !string.Equals(powerState, VmPowerStates.Deallocated, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(powerState, VmPowerStates.Running, StringComparison.OrdinalIgnoreCase)
                && tags.TryGetValue("Autoshutdown", out var autoShutdownTag)
                && string.Equals(autoShutdownTag, "1", StringComparison.OrdinalIgnoreCase))
            {
                startedAtUtc = await _vmStartTimeProvider.GetLastStartTimeUtcAsync(
                    subscriptionId,
                    vm.Id.ToString(),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get instance view for VM {VmName}", vm.Data.Name);
        }

        return new VmInfo
        {
            SubscriptionId = subscriptionId,
            ResourceGroup = vm.Id.ResourceGroupName ?? string.Empty,
            Name = string.IsNullOrWhiteSpace(vm.Data.OSProfile?.ComputerName) ? vm.Data.Name : vm.Data.OSProfile.ComputerName,
            ResourceName = vm.Data.Name,
            PowerState = powerState,
            IsAllocated = isAllocated,
            StartedAtUtc = startedAtUtc,
            Tags = tags
        };
    }
}
