using System.Collections.Concurrent;
using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Services.Interfaces;

namespace AzureVmAutoscheduler.Services;

public sealed class VmRuntimeTracker : IVmRuntimeTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _runningSinceUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly IVmRuntimeStateStore _stateStore;
    private readonly ILogger<VmRuntimeTracker> _logger;
    private int _initialized;

    public VmRuntimeTracker(IVmRuntimeStateStore stateStore, ILogger<VmRuntimeTracker> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        IReadOnlyCollection<VmRuntimeStateEntry> entries = await _stateStore.LoadAsync(cancellationToken);
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.VmKey))
            {
                _runningSinceUtc[entry.VmKey] = entry.FirstSeenRunningUtc;
            }
        }

        _logger.LogInformation("Loaded runtime state for {Count} VM(s).", _runningSinceUtc.Count);
    }

    public DateTime? GetRunningSinceUtc(string vmKey)
    {
        return _runningSinceUtc.TryGetValue(vmKey, out var value) ? value : null;
    }

    public void UpdateState(string vmKey, bool isRunning, DateTime utcNow, DateTime? startedAtUtc = null)
    {
        if (!isRunning)
        {
            _runningSinceUtc.TryRemove(vmKey, out _);
            return;
        }

        if (startedAtUtc.HasValue)
        {
            _runningSinceUtc.AddOrUpdate(
                vmKey,
                startedAtUtc.Value,
                (_, existing) => startedAtUtc.Value > existing ? startedAtUtc.Value : existing);

            return;
        }

        _runningSinceUtc.TryAdd(vmKey, utcNow);
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

    public Task PersistAsync(CancellationToken cancellationToken)
    {
        return _stateStore.SaveAsync(Snapshot(), cancellationToken);
    }

    public IReadOnlyCollection<VmRuntimeStateEntry> Snapshot()
    {
        return _runningSinceUtc
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new VmRuntimeStateEntry
            {
                VmKey = x.Key,
                FirstSeenRunningUtc = x.Value
            })
            .ToArray();
    }
}
