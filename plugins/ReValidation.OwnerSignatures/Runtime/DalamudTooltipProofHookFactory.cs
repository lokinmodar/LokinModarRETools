using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ReValidation.Common.Proof;
using ReValidation.OwnerSignatures.Services;

namespace ReValidation.OwnerSignatures.Runtime;

public sealed class DalamudTooltipProofHookFactory(IGameInteropProvider gameInteropProvider, ISignatureResolutionProvider resolutions, ulong searchBase) : ITooltipProofHookFactory
{
    private readonly IGameInteropProvider gameInteropProvider = gameInteropProvider;
    private readonly ISignatureResolutionProvider resolutions = resolutions;

    public ITooltipProofHook Create(string targetId)
    {
        var id = targetId == "AddonItemDetail.GenerateTooltip" ? "itemTooltip" : "actionTooltip";
        var resolution = resolutions.GetResolution(id);
        if (resolution.MatchCount != 1 || resolution.Rva is null)
            throw new InvalidOperationException("Signature resolution was not unique.");
        return new TooltipProofHook(gameInteropProvider, checked((nint)(searchBase + resolution.Rva.Value)));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GenerateTooltipDelegate(nint addon, nint numberArray, nint stringArray);
    private sealed class TooltipProofHook : ITooltipProofHook
    {
        private readonly Hook<GenerateTooltipDelegate> hook;
        private int hits;
        public TooltipProofHook(IGameInteropProvider provider, nint address) => hook = provider.HookFromAddress<GenerateTooltipDelegate>(address, Detour);
        public int ObservedHitCount => Volatile.Read(ref hits);
        public void Enable() => hook.Enable();
        public void Dispose() => hook.Dispose();
        private void Detour(nint addon, nint numberArray, nint stringArray) { Interlocked.Increment(ref hits); hook.Original(addon, numberArray, stringArray); }
    }
}
