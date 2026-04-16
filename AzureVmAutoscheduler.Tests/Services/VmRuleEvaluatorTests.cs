using AzureVmAutoscheduler.Models;
using AzureVmAutoscheduler.Options;
using AzureVmAutoscheduler.Services;
using Microsoft.Extensions.Options;

namespace AzureVmAutoscheduler.Tests.Services;

[TestFixture]
public class VmRuleEvaluatorTests
{
    private static VmRuleEvaluator CreateEvaluator(int shutdownAfterHours = 8)
    {
        return new VmRuleEvaluator(Microsoft.Extensions.Options.Options.Create(new AppOptions
        {
            ShutdownAfterHours = shutdownAfterHours
        }));
    }

    [Test]
    public void Evaluate_ReturnsNone_WhenAutoshutdownTagMissing()
    {
        var evaluator = CreateEvaluator();
        var vm = new VmInfo
        {
            PowerState = "running",
            RunningSinceUtc = DateTime.UtcNow.AddHours(-10)
        };

        var action = evaluator.Evaluate(vm, DateTime.UtcNow);

        Assert.That(action, Is.EqualTo(VmActionType.None));
    }

    [Test]
    public void Evaluate_ReturnsShutdown_WhenRunningLongerThanThreshold()
    {
        var evaluator = CreateEvaluator();
        var now = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc);
        var vm = new VmInfo
        {
            PowerState = "running",
            RunningSinceUtc = now.AddHours(-9),
            Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Autoshutdown"] = "1"
            }
        };

        var action = evaluator.Evaluate(vm, now);

        Assert.That(action, Is.EqualTo(VmActionType.Shutdown));
    }

    [Test]
    public void Evaluate_ReturnsDeallocate_WhenStoppedButStillAllocated()
    {
        var evaluator = CreateEvaluator();
        var vm = new VmInfo
        {
            PowerState = "stopped",
            IsAllocated = true,
            Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Autoshutdown"] = "1"
            }
        };

        var action = evaluator.Evaluate(vm, DateTime.UtcNow);

        Assert.That(action, Is.EqualTo(VmActionType.Deallocate));
    }

    [Test]
    public void Evaluate_ReturnsNone_WhenStoppedAndNotAllocated()
    {
        var evaluator = CreateEvaluator();
        var vm = new VmInfo
        {
            PowerState = "stopped",
            IsAllocated = false,
            Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Autoshutdown"] = "1"
            }
        };

        var action = evaluator.Evaluate(vm, DateTime.UtcNow);

        Assert.That(action, Is.EqualTo(VmActionType.None));
    }

    [Test]
    public void Evaluate_ReturnsNone_WhenRunningDurationEqualsThreshold()
    {
        var evaluator = CreateEvaluator();
        var now = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc);
        var vm = new VmInfo
        {
            PowerState = "running",
            RunningSinceUtc = now.AddHours(-8),
            Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Autoshutdown"] = "1"
            }
        };

        var action = evaluator.Evaluate(vm, now);

        Assert.That(action, Is.EqualTo(VmActionType.None));
    }
}
