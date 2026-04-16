namespace AzureVmAutoscheduler.Models;

public sealed class VmInfo
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PowerState { get; set; } = string.Empty;
    public bool IsAllocated { get; set; }
    public DateTime? RunningSinceUtc { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasAutoshutdownTag =>
        Tags.TryGetValue("Autoshutdown", out var value) &&
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
}
