using AzureVmAutoscheduler.Models;

namespace AzureVmAutoscheduler.Services.Interfaces;

public interface IVmRuntimeTracker
{
    Task InitializeAsync(CancellationToken cancellationToken);
    DateTime? GetRunningSinceUtc(string vmKey);
    void UpdateState(string vmKey, string powerState, DateTime utcNow);
    void RemoveMissing(IEnumerable<string> activeVmKeys);
    Task PersistAsync(CancellationToken cancellationToken);
    IReadOnlyCollection<VmRuntimeStateEntry> Snapshot();
}
