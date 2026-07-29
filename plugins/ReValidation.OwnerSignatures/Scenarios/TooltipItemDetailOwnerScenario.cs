using System.Text.Json.Nodes;
using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;
using ReValidation.Common.Scenarios;
using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Scenarios;

public sealed class TooltipItemDetailOwnerScenario(
    ITooltipProbe probe,
    OwnerHookProofExecutor? proofExecutor = null,
    ITooltipComparisonSource? comparisonSource = null,
    string? runtimeBlockingReason = null) : OwnerTooltipValidationScenarioBase(
        probe,
        proofExecutor,
        comparisonSource,
        runtimeBlockingReason,
        "itemTooltip",
        "item",
        new ValidationScenarioDefinition(
            "tooltip.item-detail",
            "Tooltip Item Detail",
            "Open an item tooltip.",
            [ValidationRoute.OwnerSignatures]));

public abstract class OwnerTooltipValidationScenarioBase : IValidationScenario, IArmableValidationScenario
{
    private readonly ITooltipProbe probe;
    private readonly ITooltipComparisonSource? comparisonSource;
    private readonly string detailKind;
    private readonly OwnerHookProofExecutor? proofExecutor;
    private readonly string? runtimeBlockingReason;
    private readonly string targetId;
    private TooltipSnapshot? capturedSnapshot;
    private OwnerHookSession? session;

    protected OwnerTooltipValidationScenarioBase(
        ITooltipProbe probe,
        OwnerHookProofExecutor? proofExecutor,
        ITooltipComparisonSource? comparisonSource,
        string? runtimeBlockingReason,
        string targetId,
        string detailKind,
        ValidationScenarioDefinition definition)
    {
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.proofExecutor = proofExecutor;
        this.comparisonSource = comparisonSource;
        this.runtimeBlockingReason = runtimeBlockingReason;
        this.targetId = targetId;
        this.detailKind = detailKind;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public ValidationScenarioDefinition Definition { get; }

    public string ArmPrompt => $"Arm the scenario, then {Definition.Description}";

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(runtimeBlockingReason))
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, runtimeBlockingReason));

        if (proofExecutor is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Owner tooltip hook proof executor is required."));

        if (context.Mode is ValidationMode.Compare or ValidationMode.FullProof && comparisonSource is null)
            return ValueTask.FromResult(new ScenarioPreconditionResult(false, "Tooltip comparison reference source is required."));

        return ValueTask.FromResult(new ScenarioPreconditionResult(true, null));
    }

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

    public async ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var activeSession = await proofExecutor!.CaptureAsync(targetId, cancellationToken);
        try
        {
            var snapshot = await probe.CaptureAsync(cancellationToken);
            if (!string.Equals(snapshot.DetailKind, detailKind, StringComparison.Ordinal))
                throw new InvalidOperationException($"Tooltip detail kind mismatch: expected '{detailKind}' but probe captured '{snapshot.DetailKind}'.");

            capturedSnapshot = snapshot;
            var capture = new ScenarioCapture(
                $"{detailKind} tooltip captured",
                new JsonObject
                {
                    ["detailKind"] = detailKind,
                    ["resolvedId"] = snapshot.ResolvedId,
                    ["ownerHook"] = activeSession.BuildEvidence().ToJson(),
                });

            if (context.Mode is ValidationMode.CaptureOnly or ValidationMode.Compare)
            {
                DisposeSession(activeSession);
                capture.Data["ownerHook"] = activeSession.BuildEvidence().ToJson();
                return capture;
            }

            session = activeSession;
            return capture;
        }
        catch
        {
            activeSession.Dispose();
            throw;
        }
    }

    public async ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
    {
        if (capturedSnapshot is null || comparisonSource is null)
            return new ScenarioCompareResult(false, "Tooltip comparison reference unavailable", ["A typed comparison reference is required."]);

        try
        {
            var reference = await comparisonSource.CaptureReferenceAsync(cancellationToken);
            var comparison = TooltipComparer.Compare(capturedSnapshot, reference);
            if (!comparison.IsMatch)
                Disarm(capture);

            return comparison;
        }
        catch
        {
            Disarm(capture);
            throw;
        }
    }

    public async ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken)
    {
        var ticket = await RequireSession().MutationStrategy.ApplyAsync(RequireSession(), cancellationToken);
        UpdateEvidence(capture);
        return ticket;
    }

    public async ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
    {
        var assertion = await RequireSession().MutationStrategy.AssertAsync(RequireSession(), cancellationToken);
        UpdateEvidence(capture);
        return assertion;
    }

    public async ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken)
    {
        if (session is null)
            return new ScenarioRestoreResult(true, "No tooltip hook session was active.", []);

        var activeSession = session;
        session = null;
        try
        {
            var restore = await activeSession.MutationStrategy.RestoreAsync(activeSession, cancellationToken);
            capture.Data["ownerHook"] = activeSession.BuildEvidence().ToJson();
            return restore;
        }
        finally
        {
            activeSession.Dispose();
        }
    }

    private OwnerHookSession RequireSession() =>
        session ?? throw new InvalidOperationException("Tooltip hook session was not captured.");

    private void UpdateEvidence(ScenarioCapture capture)
    {
        if (session is not null)
            capture.Data["ownerHook"] = session.BuildEvidence().ToJson();
    }

    private void Disarm(ScenarioCapture capture)
    {
        if (session is null)
            return;

        var activeSession = session;
        session = null;
        DisposeSession(activeSession);
        capture.Data["ownerHook"] = activeSession.BuildEvidence().ToJson();
    }

    private static void DisposeSession(OwnerHookSession activeSession)
    {
        activeSession.AddStage(new OwnerHookProofRecord(
            OwnerHookProofStage.RestoreAttempted,
            OwnerHookProofStatus.Passed,
            "Tooltip hook session disposed.",
            new JsonObject()));
        activeSession.Dispose();
    }
}
