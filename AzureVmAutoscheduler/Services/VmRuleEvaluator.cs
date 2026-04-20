using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Services;

public sealed class VmRuleEvaluator
{
    private readonly AppOptions _options;

    public VmRuleEvaluator(IOptions<AppOptions> options)
    {
        _options = options.Value;
    }

    public VmActionType Evaluate(VmInfo vm, DateTime utcNow)
    {
        if (!vm.HasAutoshutdownTag)
        {
            return VmActionType.None;
        }

        if (string.Equals(vm.PowerState, VmPowerStates.Running, StringComparison.OrdinalIgnoreCase)
            && vm.RunningSinceUtc.HasValue)
        {
            var runningFor = utcNow - vm.RunningSinceUtc.Value;
            if (runningFor > TimeSpan.FromHours(_options.ShutdownAfterHours))
            {
                return VmActionType.Shutdown;
            }
        }

        if (string.Equals(vm.PowerState, VmPowerStates.Stopped, StringComparison.OrdinalIgnoreCase) && vm.IsAllocated)
        {
            return VmActionType.Deallocate;
        }

        return VmActionType.None;
    }
}
