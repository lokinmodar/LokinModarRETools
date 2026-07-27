using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.Common.Scenarios;

public sealed record TooltipSnapshot(string DetailKind, uint ResolvedId, IReadOnlyList<string> PayloadLines, string VisibleText);

public interface ITooltipProbe
{
    ValueTask<TooltipSnapshot> CaptureAsync(CancellationToken cancellationToken);
    ValueTask<ScenarioOverrideTicket?> ApplySentinelOverrideAsync(string sentinel, CancellationToken cancellationToken);
    ValueTask<ScenarioAssertResult?> AssertSentinelAsync(string sentinel, CancellationToken cancellationToken);
    ValueTask<ScenarioRestoreResult> RestoreAsync(CancellationToken cancellationToken);
}

public interface ITooltipComparisonSource
{
    /// <summary>
    /// Captures the independent route snapshot used as comparison proof.
    /// </summary>
    ValueTask<TooltipSnapshot> CaptureReferenceAsync(CancellationToken cancellationToken);
}

public abstract class TooltipValidationScenarioBase : IValidationScenario, IArmableValidationScenario
{
    protected const string Sentinel = "[REVALIDATION] Tooltip Sentinel";
    private readonly ITooltipProbe probe;
    private readonly ITooltipComparisonSource? comparisonSource;
    private readonly string detailKind;
    private readonly string? runtimeBlockingReason;
    private TooltipSnapshot? capturedSnapshot;

    protected TooltipValidationScenarioBase(
        ITooltipProbe probe,
        ValidationScenarioDefinition definition,
        string detailKind,
        ITooltipComparisonSource? comparisonSource = null,
        string? runtimeBlockingReason = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(detailKind);

        this.probe = probe;
        this.detailKind = detailKind;
        this.comparisonSource = comparisonSource;
        this.runtimeBlockingReason = runtimeBlockingReason;
        Definition = definition;
    }

    public ValidationScenarioDefinition Definition { get; }
    public string ArmPrompt => $"Arm the scenario, then {Definition.Description}";

    public abstract ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken);

    public async ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await probe.CaptureAsync(cancellationToken);
            return string.Equals(snapshot.DetailKind, detailKind, StringComparison.Ordinal)
                ? new ScenarioArmState(true, "Tooltip cue ready.")
                : new ScenarioArmState(false, $"Waiting for {detailKind} tooltip cue.");
        }
        catch (InvalidOperationException exception)
        {
            return new ScenarioArmState(false, exception.Message);
        }
    }

    protected ScenarioPreconditionResult? GetRuntimePreconditionFailure()
    {
        if (!string.IsNullOrWhiteSpace(runtimeBlockingReason))
            return new ScenarioPreconditionResult(false, runtimeBlockingReason);

        return null;
    }

    protected ScenarioPreconditionResult? GetComparisonPreconditionFailure(ScenarioExecutionContext context)
    {
        if (context.Mode is ValidationMode.Compare or ValidationMode.FullProof && comparisonSource is null)
            return new ScenarioPreconditionResult(false, "Tooltip comparison reference source is required.");

        return null;
    }

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var snapshot = await probe.CaptureAsync(cancellationToken);
        if (!string.Equals(snapshot.DetailKind, detailKind, StringComparison.Ordinal))
            throw new InvalidOperationException($"Tooltip detail kind mismatch: expected '{detailKind}' but probe captured '{snapshot.DetailKind}'.");

        capturedSnapshot = snapshot;
        return new ScenarioCapture(
            $"{detailKind} tooltip captured",
            new JsonObject
            {
                ["detailKind"] = detailKind,
                ["resolvedId"] = snapshot.ResolvedId,
            });
    }

    public async ValueTask<ScenarioCompareResult?> CompareAsync(
        ScenarioExecutionContext context,
        ScenarioCapture capture,
        CancellationToken cancellationToken)
    {
        if (capturedSnapshot is null || comparisonSource is null)
            return new ScenarioCompareResult(false, "Tooltip comparison reference unavailable", ["A typed comparison reference is required."]);

        var reference = await comparisonSource.CaptureReferenceAsync(cancellationToken);
        return TooltipComparer.Compare(capturedSnapshot, reference);
    }

    public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) =>
        probe.ApplySentinelOverrideAsync(Sentinel, cancellationToken);

    public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        probe.AssertSentinelAsync(Sentinel, cancellationToken);

    public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) =>
        probe.RestoreAsync(cancellationToken);
}
