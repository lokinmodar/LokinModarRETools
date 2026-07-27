using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ReValidation.OwnerSignatures.Runtime;

internal static unsafe class TooltipAddonGuard
{
    public static AtkUnitBase* RequireVisibleAndReady(nint address, string addonName)
    {
        if (address == nint.Zero)
            throw new InvalidOperationException($"{addonName} addon is not visible.");

        var unitBase = (AtkUnitBase*)address;
        if (!unitBase->IsVisible || !unitBase->IsReady)
            throw new InvalidOperationException($"{addonName} addon is not visible.");

        return unitBase;
    }
}
