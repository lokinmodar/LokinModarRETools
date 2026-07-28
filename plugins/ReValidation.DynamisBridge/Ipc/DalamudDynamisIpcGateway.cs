using Dalamud.Plugin;

namespace ReValidation.DynamisBridge.Ipc;

public sealed class DalamudDynamisIpcGateway : IDynamisIpcGateway
{
    private readonly IDalamudPluginInterface pluginInterface;

    public DalamudDynamisIpcGateway(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public int? TryGetApiVersion() =>
        pluginInterface.GetIpcSubscriber<int>("Dynamis.GetApiVersion").InvokeFunc();

    public IDisposable SubscribeApiInitialized(Action handler) =>
        Subscribe("Dynamis.ApiInitialized", handler);

    public IDisposable SubscribeApiDisposing(Action handler) =>
        Subscribe("Dynamis.ApiDisposing", handler);

    public bool TryInspectObject(nint address)
    {
        pluginInterface.GetIpcSubscriber<nint, object?>("Dynamis.InspectObject.V3").InvokeAction(address);
        return true;
    }

    public bool TryInspectRegion(nint address, nuint size)
    {
        pluginInterface.GetIpcSubscriber<nint, nuint, object?>("Dynamis.InspectRegion.V2").InvokeAction(address, size);
        return true;
    }

    public string? TryGetClassName(nint address) =>
        pluginInterface.GetIpcSubscriber<nint, string?>("Dynamis.GetClass.V1").InvokeFunc(address);

    public bool TryIsInstanceOf(nint address, string className) =>
        pluginInterface.GetIpcSubscriber<nint, string, bool>("Dynamis.IsInstanceOf.V1").InvokeFunc(address, className);

    public bool TryDrawPointer(string label, nint address)
    {
        pluginInterface.GetIpcSubscriber<string, nint, bool>("Dynamis.ImGuiDrawPointer.V4").InvokeFunc(label, address);
        return true;
    }

    public void Dispose()
    {
    }

    private IDisposable Subscribe(string name, Action handler)
    {
        var subscriber = pluginInterface.GetIpcSubscriber<object?>(name);
        subscriber.Subscribe(handler);
        return new IpcSubscription(() => subscriber.Unsubscribe(handler));
    }

    private sealed class IpcSubscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
