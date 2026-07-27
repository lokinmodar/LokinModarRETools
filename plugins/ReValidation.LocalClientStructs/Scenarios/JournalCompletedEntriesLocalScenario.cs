using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.LocalClientStructs.Services;

namespace ReValidation.LocalClientStructs.Scenarios;

public sealed class JournalCompletedEntriesLocalScenario(
    IJournalCompletedEntriesProbe probe,
    LocalClientStructsAvailabilityDetector? availabilityDetector = null,
    IJournalCompletedEntriesComparisonSource? comparisonSource = null,
    string? runtimeBlockingReason = null) : IValidationScenario
{
    private const string Sentinel = "[REVALIDATION] Journal Sentinel";
    private JournalCompletedEntriesSnapshot? capturedSnapshot;

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.completed-entries",
        "Journal Completed Entries",
        "Open the completed Journal list.",
        [ValidationRoute.LocalClientStructs]);

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(runtimeBlockingReason))
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, runtimeBlockingReason));

        if (availabilityDetector is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Local ClientStructs availability detector is required."));

        var availability = availabilityDetector.Evaluate(context.Mode);
        if (!availability.IsAvailable)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, availability.BlockingReason));

        if (context.Mode is ValidationMode.Compare or ValidationMode.FullProof && comparisonSource is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Journal comparison reference source is required."));

        return ValueTask.FromResult(new ScenarioPreconditionResult(
            true,
            null));
    }

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var snapshot = await probe.CaptureAsync(cancellationToken);
        capturedSnapshot = snapshot;
        return new ScenarioCapture($"{snapshot.Entries.Count} entries captured", new JsonObject { ["entryCount"] = snapshot.Entries.Count });
    }

    public async ValueTask<ScenarioCompareResult?> CompareAsync(
        ScenarioExecutionContext context,
        ScenarioCapture capture,
        CancellationToken cancellationToken)
    {
        if (capturedSnapshot is null || comparisonSource is null)
            return new ScenarioCompareResult(false, "Journal comparison reference unavailable", ["A typed comparison reference is required."]);

        var reference = await comparisonSource.CaptureReferenceAsync(cancellationToken);
        return JournalCompletedEntriesComparer.Compare(capturedSnapshot, reference);
    }

    public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        probe.ApplySentinelOverrideAsync(Sentinel, cancellationToken);

    public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        probe.AssertSentinelAsync(Sentinel, cancellationToken);

    public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        probe.RestoreAsync(cancellationToken);
}
