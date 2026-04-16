namespace AzureVmAutoscheduler.Models;

public sealed class VmLogRow
{
    public DateTime TimestampUtc { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string PowerState { get; set; } = string.Empty;
}
