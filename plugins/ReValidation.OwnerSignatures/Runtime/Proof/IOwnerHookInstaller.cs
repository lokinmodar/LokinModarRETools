using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Runtime.Proof;

public interface IOwnerHookInstaller
{
    IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution);
}
