using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class JournalCompletedEntriesOwnerScenario(
    IJournalCompletedEntriesProbe probe,
    IEnumerable<SignatureRequirement>? requirements = null,
    IEnumerable<SignatureResolution>? resolutions = null,
    SignatureGate? signatureGate = null,
    IJournalCompletedEntriesComparisonSource? comparisonSource = null,
    string? runtimeBlockingReason = null,
    bool supportsMutationProof = true,
    string? mutationBlockingReason = null) : IValidationScenario
{
    private const string Sentinel = "[REVALIDATION] Journal Sentinel";
    private readonly IReadOnlyList<SignatureRequirement> requirements = requirements?.ToArray() ?? [];
    private readonly IReadOnlyList<SignatureResolution> resolutions = resolutions?.ToArray() ?? [];
    private readonly SignatureGate signatureGate = signatureGate ?? new SignatureGate();
    private JournalCompletedEntriesSnapshot? capturedSnapshot;

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.completed-entries",
        "Journal Completed Entries",
        "Open the completed Journal list.",
        [ValidationRoute.OwnerSignatures]);

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(runtimeBlockingReason))
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, runtimeBlockingReason));

        if (requirements.Count == 0)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Journal signature requirements are required."));

        if (resolutions.Count == 0)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Journal signature resolutions are required."));

        var result = signatureGate.Evaluate(requirements, resolutions);
        var reason = result.CanRun
            ? null
            : $"Signature requirement '{result.FailingRequirementId}' is blocked: {result.FailureReason}.";

        if (result.CanRun
            && context.Mode is ValidationMode.Compare or ValidationMode.FullProof
            && comparisonSource is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Journal comparison reference source is required."));

        if (result.CanRun
            && context.Mode is ValidationMode.OverrideAssert or ValidationMode.FullProof
            && !supportsMutationProof)
            return ValueTask.FromResult(new ScenarioPreconditionResult(
                false,
                string.IsNullOrWhiteSpace(mutationBlockingReason)
                    ? "Journal override proof is not configured."
                    : mutationBlockingReason));

        return ValueTask.FromResult(new ScenarioPreconditionResult(result.CanRun, reason));
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
