using System.Collections.Concurrent;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services;
using AzureVmAutoscheduler.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Workers;

public sealed class VmPollingWorker : BackgroundService
{
    private readonly IVmInventoryProvider _inventoryProvider;
    private readonly IVmPowerActionService _powerActionService;
    private readonly ICsvLogService _csvLogService;
    private readonly IVmRuntimeTracker _runtimeTracker;
    private readonly VmRuleEvaluator _ruleEvaluator;
    private readonly ILogger<VmPollingWorker> _logger;
    private readonly AppOptions _options;

    public VmPollingWorker(
        IVmInventoryProvider inventoryProvider,
        IVmPowerActionService powerActionService,
        ICsvLogService csvLogService,
        IVmRuntimeTracker runtimeTracker,
        VmRuleEvaluator ruleEvaluator,
        IOptions<AppOptions> options,
        ILogger<VmPollingWorker> logger)
    {
        _inventoryProvider = inventoryProvider;
        _powerActionService = powerActionService;
        _csvLogService = csvLogService;
        _runtimeTracker = runtimeTracker;
        _ruleEvaluator = ruleEvaluator;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VM autoscheduler worker started in {Mode} mode.", _options.Mode);
        await _runtimeTracker.InitializeAsync(stoppingToken);

        await RunPollingCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.PollIntervalMinutes));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPollingCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        try
        {
            await _runtimeTracker.PersistAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist runtime state during shutdown.");
        }

        _logger.LogInformation("VM autoscheduler worker stopped.");
    }

    private async Task RunPollingCycleAsync(CancellationToken stoppingToken)
    {
        var utcNow = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Polling started at {UtcNow}", utcNow);

            IReadOnlyCollection<VmInfo> inventory = await _inventoryProvider.GetAllVmsAsync(stoppingToken);
            var vms = PrepareVmsForEvaluation(inventory, utcNow);

            _logger.LogInformation("Retrieved {Count} VM(s).", vms.Count);

            var logRows = vms.Select(vm => new VmLogRow
            {
                TimestampUtc = utcNow,
                SubscriptionId = vm.SubscriptionId,
                ResourceGroup = vm.ResourceGroup,
                ComputerName = vm.Name,
                PowerState = vm.PowerState
            }).ToList();

            await _csvLogService.AppendRowsAsync(logRows, stoppingToken);

            using var semaphore = new SemaphoreSlim(_options.MaxParallelism);
            var actionCounts = new ConcurrentDictionary<VmActionType, int>();
            var tasks = vms.Select(async vm =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    var action = _ruleEvaluator.Evaluate(vm, utcNow);
                    if (action == VmActionType.None)
                    {
                        return;
                    }

                    await _powerActionService.ExecuteAsync(vm, action, stoppingToken);
                    actionCounts.AddOrUpdate(action, 1, (_, count) => count + 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed processing VM {SubscriptionId}/{ResourceGroup}/{VmName}",
                        vm.SubscriptionId,
                        vm.ResourceGroup,
                        string.IsNullOrWhiteSpace(vm.ResourceName) ? vm.Name : vm.ResourceName);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            await _runtimeTracker.PersistAsync(stoppingToken);

            _logger.LogInformation(
                "Polling finished at {UtcNow}. Shutdown={ShutdownCount}, Deallocate={DeallocateCount}, TrackedRunningVms={TrackedCount}",
                DateTime.UtcNow,
                actionCounts.GetValueOrDefault(VmActionType.Shutdown),
                actionCounts.GetValueOrDefault(VmActionType.Deallocate),
                _runtimeTracker.Snapshot().Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in polling cycle.");
        }
    }

    private List<VmInfo> PrepareVmsForEvaluation(IReadOnlyCollection<VmInfo> inventory, DateTime utcNow)
    {
        var prepared = new List<VmInfo>(inventory.Count);
        var activeVmKeys = new List<string>(inventory.Count);

        foreach (var vm in inventory)
        {
            var resourceName = string.IsNullOrWhiteSpace(vm.ResourceName) ? vm.Name : vm.ResourceName;
            var vmKey = VmIdentity.BuildKey(vm.SubscriptionId, vm.ResourceGroup, resourceName);
            activeVmKeys.Add(vmKey);

            var trackedRunningSinceUtc = _runtimeTracker.GetRunningSinceUtc(vmKey);
            if (string.Equals(vm.PowerState, VmPowerStates.Running, StringComparison.OrdinalIgnoreCase))
            {
                vm.RunningSinceUtc = vm.StartedAtUtc ?? trackedRunningSinceUtc ?? utcNow;
            }
            else
            {
                vm.RunningSinceUtc = null;
            }

            _runtimeTracker.UpdateState(
                vmKey,
                string.Equals(vm.PowerState, VmPowerStates.Running, StringComparison.OrdinalIgnoreCase),
                utcNow,
                vm.StartedAtUtc);
            prepared.Add(vm);
        }

        _runtimeTracker.RemoveMissing(activeVmKeys);
        return prepared;
    }
}
