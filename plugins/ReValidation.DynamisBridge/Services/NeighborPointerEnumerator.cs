using System.Runtime.InteropServices;

namespace ReValidation.DynamisBridge.Services;

public sealed class NeighborPointerEnumerator
{
    public IReadOnlyList<nint> Enumerate(nint baseAddress, int pointerSlots)
    {
        if (baseAddress == 0 || pointerSlots <= 0)
            return Array.Empty<nint>();

        var pointers = new List<nint>();
        for (var slot = 0; slot < pointerSlots; slot++)
        {
            var pointer = Marshal.ReadIntPtr(baseAddress, slot * IntPtr.Size);
            if (pointer != 0)
                pointers.Add(pointer);
        }

        return pointers.Distinct().ToArray();
    }
}
