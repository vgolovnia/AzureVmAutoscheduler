namespace AzureVmAutoscheduler.Services.Interfaces;

public interface IVmStartTimeProvider
{
    Task<DateTime?> GetLastStartTimeUtcAsync(string subscriptionId, string resourceId, CancellationToken cancellationToken);
}
