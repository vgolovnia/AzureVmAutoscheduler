using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services;
using AzureVmAutoscheduler.Services.Interfaces;
using AzureVmAutoscheduler.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));
builder.Services.Configure<CsvOptions>(builder.Configuration.GetSection("Csv"));

builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();
builder.Services.AddSingleton(sp => new ArmClient(sp.GetRequiredService<TokenCredential>()));

builder.Services.AddSingleton<ICsvLogService, CsvLogService>();
builder.Services.AddSingleton<IVmRuntimeTracker, VmRuntimeTracker>();
builder.Services.AddSingleton<VmRuleEvaluator>();

var appOptions = builder.Configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();

if (string.Equals(appOptions.Mode, "Azure", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IVmInventoryProvider, AzureVmInventoryProvider>();
    builder.Services.AddSingleton<IVmPowerActionService, AzureVmPowerActionService>();
}
else
{
    builder.Services.AddSingleton<IVmInventoryProvider, MockVmInventoryProvider>();
    builder.Services.AddSingleton<IVmPowerActionService, MockVmPowerActionService>();
}

builder.Services.AddHostedService<VmPollingWorker>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var host = builder.Build();
await host.RunAsync();
