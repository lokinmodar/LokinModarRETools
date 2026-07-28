using System.Numerics;

namespace ReValidation.DynamisBridge.Ipc;

public interface IDynamisIpcGateway : IDisposable
{
    (uint MajorVersion, uint MinorVersion, ulong FeatureFlags)? TryGetApiVersion();
    IDisposable SubscribeApiInitialized(Action<uint, uint, ulong, Version> handler);
    IDisposable SubscribeApiDisposing(Action handler);
    bool TryInspectObject(nint address);
    bool TryInspectObject(nint address, string? name);
    bool TryInspectObject(nint address, object? @class, string? name);
    bool TryInspectRegion(nint address, uint size, string typeName, uint typeTemplateId, uint classKindId, string? name);
    (string Name, Type? Type, uint Size, uint Displacement)? TryGetClass(nint pointer);
    (bool IsInstance, uint Displacement)? TryIsInstanceOf(nint pointer, string? className, Type? type);
    bool TryDrawPointer(nint pointer, Func<object?>? @class, Func<string?>? name, string? customText, ulong flags, Vector2 size);
}
