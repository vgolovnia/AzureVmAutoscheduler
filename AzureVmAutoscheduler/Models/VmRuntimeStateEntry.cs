namespace AzureVmAutoscheduler.Models;

public sealed class VmRuntimeStateEntry
{
    public string VmKey { get; set; } = string.Empty;
    public DateTime FirstSeenRunningUtc { get; set; }
}
