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

    public int? TryGetApiVersion() =>
        InvokeOrDefault<int?>(() => pluginInterface.GetIpcSubscriber<int>("Dynamis.GetApiVersion").InvokeFunc(), null);

    public IDisposable SubscribeApiInitialized(Action handler) =>
        Subscribe("Dynamis.ApiInitialized", handler);

    public IDisposable SubscribeApiDisposing(Action handler) =>
        Subscribe("Dynamis.ApiDisposing", handler);

    public bool TryInspectObject(nint address)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface.GetIpcSubscriber<nint, object?>("Dynamis.InspectObject.V3").InvokeAction(address);
            return true;
        }, false);
    }

    public bool TryInspectRegion(nint address, nuint size)
    {
        return InvokeOrDefault(() =>
        {
            pluginInterface.GetIpcSubscriber<nint, nuint, object?>("Dynamis.InspectRegion.V2").InvokeAction(address, size);
            return true;
        }, false);
    }

    public string? TryGetClassName(nint address) =>
        InvokeOrDefault(() => pluginInterface.GetIpcSubscriber<nint, string?>("Dynamis.GetClass.V1").InvokeFunc(address), null);

    public bool TryIsInstanceOf(nint address, string className) =>
        InvokeOrDefault(() => pluginInterface.GetIpcSubscriber<nint, string, bool>("Dynamis.IsInstanceOf.V1").InvokeFunc(address, className), false);

    public bool TryDrawPointer(string label, nint address) =>
        InvokeOrDefault(() => pluginInterface.GetIpcSubscriber<string, nint, bool>("Dynamis.ImGuiDrawPointer.V4").InvokeFunc(label, address), false);

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
