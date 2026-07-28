using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;

namespace ReValidation.DynamisBridge.Ipc;

public sealed class DalamudDynamisIpcGateway : IDynamisIpcGateway
{
    private readonly IDalamudPluginInterface pluginInterface;

    public DalamudDynamisIpcGateway(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public (uint MajorVersion, uint MinorVersion, ulong FeatureFlags)? TryGetApiVersion() =>
        InvokeOrDefault<(uint, uint, ulong)?>(
            () => pluginInterface.GetIpcSubscriber<(uint, uint, ulong)>("Dynamis.GetApiVersion").InvokeFunc(),
            null);

    public IDisposable SubscribeApiInitialized(Action<uint, uint, ulong, Version> handler) =>
        Subscribe("Dynamis.ApiInitialized", handler);

    public IDisposable SubscribeApiDisposing(Action handler) =>
        Subscribe("Dynamis.ApiDisposing", handler);

    public bool TryInspectObject(nint address)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface.GetIpcSubscriber<nint, object?>("Dynamis.InspectObject.V1").InvokeAction(address);
            return true;
        }, false);
    }

    public bool TryInspectObject(nint address, string? name)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface.GetIpcSubscriber<nint, string?, object?>("Dynamis.InspectObject.V2").InvokeAction(address, name);
            return true;
        }, false);
    }

    public bool TryInspectObject(nint address, object? @class, string? name)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface
                .GetIpcSubscriber<nint, object?, string?, object?>("Dynamis.InspectObject.V3")
                .InvokeAction(address, @class, name);
            return true;
        }, false);
    }

    public bool TryInspectRegion(nint address, uint size, string typeName, uint typeTemplateId, uint classKindId, string? name)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface
                .GetIpcSubscriber<nint, uint, string, uint, uint, string?, object?>("Dynamis.InspectRegion.V2")
                .InvokeAction(address, size, typeName, typeTemplateId, classKindId, name);
            return true;
        }, false);
    }

    public (string Name, Type? Type, uint Size, uint Displacement)? TryGetClass(nint pointer) =>
        InvokeOrDefault<(string, Type?, uint, uint)?>(
            () => pluginInterface
                .GetIpcSubscriber<nint, (string, Type?, uint, uint)>("Dynamis.GetClass.V1")
                .InvokeFunc(pointer),
            null);

    public (bool IsInstance, uint Displacement)? TryIsInstanceOf(nint pointer, string? className, Type? type) =>
        InvokeOrDefault<(bool, uint)?>(
            () => pluginInterface
                .GetIpcSubscriber<nint, string?, Type?, (bool, uint)>("Dynamis.IsInstanceOf.V1")
                .InvokeFunc(pointer, className, type),
            null);

    public bool TryDrawPointer(
        nint pointer,
        Func<object?>? @class,
        Func<string?>? name,
        string? customText,
        ulong flags,
        Vector2 size)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface
                .GetIpcSubscriber<nint, Func<object?>?, Func<string?>?, string?, ulong, Vector2, object?>("Dynamis.ImGuiDrawPointer.V4")
                .InvokeAction(pointer, @class, name, customText, flags, size);
            return true;
        }, false);
    }

    public void Dispose()
    {
    }

    private IDisposable Subscribe(string name, Action handler)
    {
        try
        {
            var subscriber = pluginInterface.GetIpcSubscriber<object?>(name);
            subscriber.Subscribe(handler);
            return new IpcSubscription(() => subscriber.Unsubscribe(handler));
        }
        catch (IpcNotReadyError)
        {
            return EmptySubscription.Instance;
        }
    }

    private IDisposable Subscribe(string name, Action<uint, uint, ulong, Version> handler)
    {
        try
        {
            var subscriber = pluginInterface.GetIpcSubscriber<uint, uint, ulong, Version, object?>(name);
            subscriber.Subscribe(handler);
            return new IpcSubscription(() => subscriber.Unsubscribe(handler));
        }
        catch (IpcNotReadyError)
        {
            return EmptySubscription.Instance;
        }
    }

    private static T? InvokeOrDefault<T>(Func<T> invoke, T? fallback)
    {
        try
        {
            return invoke();
        }
        catch (IpcNotReadyError)
        {
            return fallback;
        }
    }

    private sealed class IpcSubscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }

    private sealed class EmptySubscription : IDisposable
    {
        public static EmptySubscription Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
