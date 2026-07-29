namespace ReValidation.OwnerSignatures.Runtime.Proof;

public enum OwnerHookProofStage
{
    SignatureResolved,
    HookInstalled,
    HitObserved,
    ContextCaptured,
    MutationAttempted,
    EffectAsserted,
    RestoreAttempted,
}
