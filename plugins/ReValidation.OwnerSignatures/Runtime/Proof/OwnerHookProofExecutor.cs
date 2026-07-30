using System.Text.Json.Nodes;
using ReValidation.Common.Models;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed class OwnerHookProofExecutor(
    OwnerHookTargetRegistry registry,
    IOwnerHookInstaller installer,
    ISignatureResolutionProvider resolutions)
{
    public ScenarioPreconditionResult ValidateTarget(string targetId)
    {
        var target = registry.Get(targetId);
        var resolution = resolutions.GetResolution(target.SignatureId);
        return resolution.MatchCount == 1 && resolution.Rva is not null
            ? new ScenarioPreconditionResult(true, null)
            : new ScenarioPreconditionResult(false, $"Signature '{target.SignatureId}' was not uniquely resolved.");
    }

    public ValueTask<OwnerHookSession> CaptureAsync(string targetId, CancellationToken cancellationToken)
    {
        var session = ArmAsync(targetId, cancellationToken);
        try
        {
            return CaptureArmedAsync(session, cancellationToken);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public OwnerHookSession ArmAsync(string targetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = registry.Get(targetId);
        var resolution = resolutions.GetResolution(target.SignatureId);
        var precondition = ValidateTarget(targetId);
        if (!precondition.CanRun)
            throw new InvalidOperationException(precondition.BlockingReason);

        var hook = installer.Install(target, resolution);
        try
        {
            hook.Enable();
            var session = new OwnerHookSession(target.TargetId, hook, target.MutationStrategy);
            session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.SignatureResolved, OwnerHookProofStatus.Passed, "Unique owner signature resolved.", new JsonObject { ["signatureId"] = target.SignatureId }));
            session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.HookInstalled, OwnerHookProofStatus.Passed, "Owner hook installed.", new JsonObject { ["targetId"] = target.TargetId }));
            return session;
        }
        catch
        {
            hook.Dispose();
            throw;
        }
    }

    public ValueTask<OwnerHookSession> CaptureArmedAsync(OwnerHookSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        var target = registry.Get(session.TargetId);
        var captured = session.Hook.DrainObservedContexts();
        if (captured.Count == 0)
        {
            session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.HitObserved, OwnerHookProofStatus.NotObserved, "Hook installed but no runtime hit was observed.", new JsonObject()));
            return ValueTask.FromResult(session);
        }

        session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.HitObserved, OwnerHookProofStatus.Passed, "Owner hook observed runtime hits.", new JsonObject { ["observedHitCount"] = session.Hook.ObservedHitCount }));
        var sanitized = target.ContextCapture.Capture(captured[0]);
        if (sanitized.Count == 0)
        {
            session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.ContextCaptured, OwnerHookProofStatus.NotObserved, "Owner hook context did not contain an allowlisted value.", new JsonObject()));
            return ValueTask.FromResult(session);
        }

        session.SetCapturedContext(sanitized);
        session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.ContextCaptured, OwnerHookProofStatus.Passed, "Owner hook context captured.", sanitized.DeepClone().AsObject()));
        return ValueTask.FromResult(session);
    }
}
