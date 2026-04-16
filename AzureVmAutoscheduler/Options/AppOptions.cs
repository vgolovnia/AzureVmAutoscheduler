namespace AzureVmAutoscheduler.Options;

public sealed class AppOptions
{
    public string Mode { get; set; } = "Mock";
    public int PollIntervalMinutes { get; set; } = 5;
    public int MaxParallelism { get; set; } = 5;
    public int ShutdownAfterHours { get; set; } = 8;
    public string MockDataFile { get; set; } = "mock-vms.json";
}
