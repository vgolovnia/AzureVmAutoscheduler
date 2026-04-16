using System.Collections.Concurrent;
using AzureVmAutoscheduler.Services.Interfaces;

namespace AzureVmAutoscheduler.Services;

public sealed class VmRuntimeTracker : IVmRuntimeTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _runningSinceUtc = new(StringComparer.OrdinalIgnoreCase);

    public DateTime? GetRunningSinceUtc(string vmKey)
    {
        return _runningSinceUtc.TryGetValue(vmKey, out var value) ? value : null;
    }

    public void UpdateState(string vmKey, string powerState, DateTime utcNow)
    {
        if (string.Equals(powerState, "running", StringComparison.OrdinalIgnoreCase))
        {
            _runningSinceUtc.TryAdd(vmKey, utcNow);
            return;
        }

        _runningSinceUtc.TryRemove(vmKey, out _);
    }

    public void RemoveMissing(IEnumerable<string> activeVmKeys)
    {
        var active = new HashSet<string>(activeVmKeys, StringComparer.OrdinalIgnoreCase);

        foreach (var key in _runningSinceUtc.Keys)
        {
            if (!active.Contains(key))
            {
                _runningSinceUtc.TryRemove(key, out _);
            }
        }
    }
}
