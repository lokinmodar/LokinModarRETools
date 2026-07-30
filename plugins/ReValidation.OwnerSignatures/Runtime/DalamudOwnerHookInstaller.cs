using ReValidation.OwnerSignatures.Runtime.HookTargets;
using ReValidation.OwnerSignatures.Runtime.Proof;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Runtime;

public sealed class DalamudOwnerHookInstaller(ulong moduleBase) : IOwnerHookInstaller
{
    public IOwnerHook Install(OwnerHookTargetDefinition target, SignatureResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolution);

        if (resolution.Rva is null)
            throw new InvalidOperationException($"Owner hook target '{target.TargetId}' did not include an RVA.");

        var targetAddress = checked((nint)(moduleBase + resolution.Rva.Value));
        return target.Binding.Install(targetAddress);
    }
}
