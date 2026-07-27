using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs.Scenarios;

public sealed class JournalCompletedEntriesLocalScenario(
    IJournalCompletedEntriesProbe probe,
    LocalClientStructsAvailabilityDetector? availabilityDetector = null) : IValidationScenario
{
    private const string Sentinel = "[REVALIDATION] Journal Sentinel";

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.completed-entries",
        "Journal Completed Entries",
        "Open the completed Journal list.",
        [ValidationRoute.LocalClientStructs]);

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var availability = availabilityDetector?.Evaluate(context.Mode);
        return ValueTask.FromResult(new ScenarioPreconditionResult(
            availability?.IsAvailable ?? true,
            availability?.BlockingReason));
    }

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var snapshot = await probe.CaptureAsync(cancellationToken);
        return new ScenarioCapture($"{snapshot.Entries.Count} entries captured", new JsonObject { ["entryCount"] = snapshot.Entries.Count });
    }

    public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        ValueTask.FromResult<ScenarioCompareResult?>(null);

    public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        probe.ApplySentinelOverrideAsync(Sentinel, cancellationToken);

    public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        probe.AssertSentinelAsync(Sentinel, cancellationToken);

    public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        probe.RestoreAsync(cancellationToken);
}
