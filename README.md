# Azure VM Autoscheduler

## Overview

Azure VM Autoscheduler is a .NET 8 worker service that runs continuously, polls all virtual machines across all subscriptions in the current tenant, appends inventory data to a CSV file, and applies automatic power management rules for VMs tagged with `Autoshutdown=1`.

## What it does

- Polls Azure every 5 minutes by default
- Enumerates all VMs across all subscriptions visible to the authenticated identity
- Appends one CSV row per VM for every poll cycle
- For tagged VMs only:
  - powers off VMs that have been observed running for more than 8 hours
  - deallocates VMs that are stopped but still allocated
- Continues running until manually stopped
- Handles exceptions without crashing the worker

## Configuration

`appsettings.json`

```json
{
  "App": {
    "Mode": "Mock",
    "PollIntervalMinutes": 5,
    "MaxParallelism": 5,
    "ShutdownAfterHours": 8,
    "MockDataFile": "mock-vms.json",
    "RuntimeStateFile": "vm-runtime-state.json"
  },
  "Csv": {
    "FilePath": "vm-log.csv"
  }
}
```

## Modes

### Mock mode

Default mode for local development.

```bash
dotnet run --project AzureVmAutoscheduler/AzureVmAutoscheduler.csproj
```

### Azure mode

Authenticate first with a developer login or managed identity.

```bash
az login
```

Then set:

```json
"Mode": "Azure"
```

## Notes about the 8-hour rule

Azure instance view gives reliable current power state, but not a durable VM uptime value for this use case. To keep API usage reasonable without introducing external infrastructure, the worker persists the first observed `running` timestamp for each VM to a local JSON state file and reloads it on startup.

That means:
- the rule continues to work correctly across application restarts
- no database or external cache is required
- the persisted state is updated after each polling cycle

## Azure SDK usage

The application uses official Microsoft packages:
- `Azure.Identity`
- `Azure.ResourceManager`
- `Azure.ResourceManager.Compute`

Power actions are executed through `VirtualMachineResource` operations.

## CSV output

The CSV file is append-only.

Columns:
- `TimestampUtc`
- `SubscriptionId`
- `ResourceGroup`
- `ComputerName`
- `PowerState`

## Running tests

```bash
dotnet test
```
