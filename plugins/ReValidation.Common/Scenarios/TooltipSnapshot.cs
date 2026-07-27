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

public abstract class TooltipValidationScenarioBase : IValidationScenario
{
    protected const string Sentinel = "[REVALIDATION] Tooltip Sentinel";
    private readonly ITooltipProbe probe;
    private readonly string detailKind;

    protected TooltipValidationScenarioBase(ITooltipProbe probe, ValidationScenarioDefinition definition, string detailKind)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(detailKind);

        this.probe = probe;
        this.detailKind = detailKind;
        Definition = definition;
    }

    public ValidationScenarioDefinition Definition { get; }

    public abstract ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken);

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var snapshot = await probe.CaptureAsync(cancellationToken);
        if (!string.Equals(snapshot.DetailKind, detailKind, StringComparison.Ordinal))
            throw new InvalidOperationException($"Tooltip detail kind mismatch: expected '{detailKind}' but probe captured '{snapshot.DetailKind}'.");

        return new ScenarioCapture(
            $"{detailKind} tooltip captured",
            new JsonObject
            {
                ["detailKind"] = detailKind,
                ["resolvedId"] = snapshot.ResolvedId,
            });
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
