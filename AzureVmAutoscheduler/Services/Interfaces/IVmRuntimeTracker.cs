namespace AzureVmAutoscheduler.Services.Interfaces;

public interface IVmRuntimeTracker
{
    DateTime? GetRunningSinceUtc(string vmKey);
    void UpdateState(string vmKey, string powerState, DateTime utcNow);
    void RemoveMissing(IEnumerable<string> activeVmKeys);
}
