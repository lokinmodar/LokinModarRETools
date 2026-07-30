using ReValidation.OwnerSignatures.Runtime.Proof;

namespace ReValidation.OwnerSignatures.Runtime.HookTargets;

public sealed record OwnerHookTargetDefinition(
    string TargetId,
    string SignatureId,
    string CueDescription,
    IHookContextCapture ContextCapture,
    IHookMutationStrategy MutationStrategy,
    IOwnerHookBinding Binding);

public interface IOwnerHookBinding
{
    IOwnerHook Install(nint targetAddress);
}
