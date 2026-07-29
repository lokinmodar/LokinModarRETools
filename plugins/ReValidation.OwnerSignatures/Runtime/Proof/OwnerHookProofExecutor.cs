using System.Text.Json.Nodes;
using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public sealed class OwnerHookProofExecutor(
    OwnerHookTargetRegistry registry,
    IOwnerHookInstaller installer,
    ISignatureResolutionProvider resolutions)
{
    public ValueTask<OwnerHookSession> CaptureAsync(string targetId, CancellationToken cancellationToken)
    {
        var target = registry.Get(targetId);
        var resolution = resolutions.GetResolution(target.SignatureId);
        if (resolution.MatchCount != 1 || resolution.Rva is null)
            throw new InvalidOperationException($"Signature '{target.SignatureId}' was not uniquely resolved.");

        var hook = installer.Install(target, resolution);
        hook.Enable();
        var session = new OwnerHookSession(target.TargetId, hook, target.MutationStrategy);
        session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.SignatureResolved, OwnerHookProofStatus.Passed, "Unique owner signature resolved.", new JsonObject { ["signatureId"] = target.SignatureId }));
        session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.HookInstalled, OwnerHookProofStatus.Passed, "Owner hook installed.", new JsonObject { ["targetId"] = target.TargetId }));

        cancellationToken.ThrowIfCancellationRequested();
        var captured = hook.DrainObservedContexts();
        if (captured.Count == 0)
        {
            session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.HitObserved, OwnerHookProofStatus.NotObserved, "Hook installed but no runtime hit was observed.", new JsonObject()));
            return ValueTask.FromResult(session);
        }

        session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.HitObserved, OwnerHookProofStatus.Passed, "Owner hook observed runtime hits.", new JsonObject { ["observedHitCount"] = hook.ObservedHitCount }));
        var sanitized = target.ContextCapture.Capture(captured[0]);
        session.SetCapturedContext(sanitized);
        session.AddStage(new OwnerHookProofRecord(OwnerHookProofStage.ContextCaptured, OwnerHookProofStatus.Passed, "Owner hook context captured.", sanitized.DeepClone().AsObject()));
        return ValueTask.FromResult(session);
    }
}
