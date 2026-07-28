using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using ReValidation.Common.Proof;

namespace ReValidation.LocalClientStructs.Runtime;

public sealed class DalamudTooltipProofHookFactory(IGameInteropProvider gameInteropProvider) : ITooltipProofHookFactory
{
    private readonly IGameInteropProvider gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));

    public ITooltipProofHook Create(string targetId) => targetId switch
    {
        "AddonItemDetail.GenerateTooltip" => new TooltipProofHook(gameInteropProvider, AddonItemDetail.Addresses.GenerateTooltip.Value),
        "AddonActionDetail.GenerateTooltip" => new TooltipProofHook(gameInteropProvider, AddonActionDetail.Addresses.GenerateTooltip.Value),
        _ => throw new InvalidOperationException($"Unsupported tooltip target '{targetId}'."),
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GenerateTooltipDelegate(nint addon, nint numberArray, nint stringArray);

    private sealed class TooltipProofHook : ITooltipProofHook
    {
        private readonly Hook<GenerateTooltipDelegate> hook;
        private int observedHitCount;

        public TooltipProofHook(IGameInteropProvider gameInteropProvider, nint address)
        {
            hook = gameInteropProvider.HookFromAddress<GenerateTooltipDelegate>(address, Detour);
        }

        public int ObservedHitCount => Volatile.Read(ref observedHitCount);
        public void Enable() => hook.Enable();
        public void Dispose() => hook.Dispose();

        private void Detour(nint addon, nint numberArray, nint stringArray)
        {
            Interlocked.Increment(ref observedHitCount);
            hook.Original(addon, numberArray, stringArray);
        }
    }
}
