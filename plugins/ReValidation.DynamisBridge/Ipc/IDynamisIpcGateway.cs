namespace ReValidation.DynamisBridge.Ipc;

public interface IDynamisIpcGateway : IDisposable
{
    int? TryGetApiVersion();
    IDisposable SubscribeApiInitialized(Action handler);
    IDisposable SubscribeApiDisposing(Action handler);
    bool TryInspectObject(nint address);
    bool TryInspectRegion(nint address, nuint size);
    string? TryGetClassName(nint address);
    bool TryIsInstanceOf(nint address, string className);
    bool TryDrawPointer(string label, nint address);
}
