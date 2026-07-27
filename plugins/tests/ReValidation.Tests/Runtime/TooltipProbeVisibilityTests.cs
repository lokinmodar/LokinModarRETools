using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace ReValidation.Tests.Runtime;

public sealed class TooltipProbeVisibilityTests
{
    [Fact]
    public unsafe void LocalItemProbe_CaptureFails_WhenAddonAddressExistsButAddonIsHidden()
    {
        var addon = (AddonItemDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AddonItemDetail>());
        var agent = (AgentItemDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AgentItemDetail>());

        try
        {
            var probe = new ReValidation.LocalClientStructs.Runtime.ItemDetailTooltipProbe(
                () => (nint)addon,
                () => (nint)agent);
            var exception = Assert.Throws<InvalidOperationException>(() => probe.CaptureAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult());

            Assert.Equal("ItemDetail addon is not visible.", exception.Message);
        }
        finally
        {
            NativeMemory.Free(addon);
            NativeMemory.Free(agent);
        }
    }

    [Fact]
    public unsafe void LocalActionProbe_CaptureFails_WhenAddonAddressExistsButAddonIsHidden()
    {
        var addon = (AddonActionDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AddonActionDetail>());
        var agent = (AgentActionDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AgentActionDetail>());

        try
        {
            var probe = new ReValidation.LocalClientStructs.Runtime.ActionDetailTooltipProbe(
                () => (nint)addon,
                () => (nint)agent);
            var exception = Assert.Throws<InvalidOperationException>(() => probe.CaptureAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult());

            Assert.Equal("ActionDetail addon is not visible.", exception.Message);
        }
        finally
        {
            NativeMemory.Free(addon);
            NativeMemory.Free(agent);
        }
    }

    [Fact]
    public unsafe void OwnerItemProbe_CaptureFails_WhenAddonAddressExistsButAddonIsHidden()
    {
        var addon = (AddonItemDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AddonItemDetail>());
        var agent = (AgentItemDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AgentItemDetail>());

        try
        {
            var probe = new ReValidation.OwnerSignatures.Runtime.ItemDetailTooltipProbe(
                () => (nint)addon,
                () => (nint)agent);
            var exception = Assert.Throws<InvalidOperationException>(() => probe.CaptureAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult());

            Assert.Equal("ItemDetail addon is not visible.", exception.Message);
        }
        finally
        {
            NativeMemory.Free(addon);
            NativeMemory.Free(agent);
        }
    }

    [Fact]
    public unsafe void OwnerActionProbe_CaptureFails_WhenAddonAddressExistsButAddonIsHidden()
    {
        var addon = (AddonActionDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AddonActionDetail>());
        var agent = (AgentActionDetail*)NativeMemory.AllocZeroed((nuint)Unsafe.SizeOf<AgentActionDetail>());

        try
        {
            var probe = new ReValidation.OwnerSignatures.Runtime.ActionDetailTooltipProbe(
                () => (nint)addon,
                () => (nint)agent);
            var exception = Assert.Throws<InvalidOperationException>(() => probe.CaptureAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult());

            Assert.Equal("ActionDetail addon is not visible.", exception.Message);
        }
        finally
        {
            NativeMemory.Free(addon);
            NativeMemory.Free(agent);
        }
    }
}
