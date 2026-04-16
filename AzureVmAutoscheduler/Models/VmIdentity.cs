namespace AzureVmAutoscheduler.Models;

public static class VmIdentity
{
    public static string BuildKey(string subscriptionId, string resourceGroup, string name)
        => $"{subscriptionId}/{resourceGroup}/{name}";
}
