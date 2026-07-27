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
    SignatureGate? signatureGate = null) : IValidationScenario
{
    private const string Sentinel = "[REVALIDATION] Journal Sentinel";
    private readonly IReadOnlyList<SignatureRequirement> requirements = requirements?.ToArray() ?? [];
    private readonly IReadOnlyList<SignatureResolution> resolutions = resolutions?.ToArray() ?? [];
    private readonly SignatureGate signatureGate = signatureGate ?? new SignatureGate();

    public ValidationScenarioDefinition Definition { get; } = new(
        "journal.completed-entries",
        "Journal Completed Entries",
        "Open the completed Journal list.",
        [ValidationRoute.OwnerSignatures]);

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var result = signatureGate.Evaluate(requirements, resolutions);
        var reason = result.CanRun
            ? null
            : $"Signature requirement '{result.FailingRequirementId}' is blocked: {result.FailureReason}.";
        return ValueTask.FromResult(new ScenarioPreconditionResult(result.CanRun, reason));
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
