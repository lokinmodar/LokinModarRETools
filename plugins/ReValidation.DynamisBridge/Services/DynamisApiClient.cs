using ReValidation.DynamisBridge.Ipc;
using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public interface IDynamisApiClient : IDisposable
{
    event Action? AvailabilityChanged;
    DynamisAvailabilitySnapshot Current { get; }
    void Refresh();
    bool InspectObject(nint address);
    bool InspectRegion(nint address, nuint size);
    string? GetClassName(nint address);
    bool IsInstanceOf(nint address, string className);
    bool DrawPointer(string label, nint address);
}

public interface IDynamisAvailabilityService
{
    DynamisAvailabilitySnapshot Current { get; }
    bool IsReady { get; }
}

public sealed class DynamisApiClient : IDynamisApiClient
{
    private readonly IDynamisIpcGateway gateway;
    private readonly int minimumApiVersion;
    private readonly IDisposable initializedSubscription;
    private readonly IDisposable disposingSubscription;

    public DynamisApiClient(IDynamisIpcGateway gateway, int minimumApiVersion)
    {
        this.gateway = gateway;
        this.minimumApiVersion = minimumApiVersion;
        initializedSubscription = gateway.SubscribeApiInitialized(Refresh);
        disposingSubscription = gateway.SubscribeApiDisposing(Refresh);
        Current = new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable.");
    }

    public event Action? AvailabilityChanged;
    public DynamisAvailabilitySnapshot Current { get; private set; }

    public void Refresh()
    {
        var version = gateway.TryGetApiVersion();
        Current = version switch
        {
            null => new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable."),
            var resolved when resolved < minimumApiVersion => new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Incompatible, version, $"Dynamis API {version} is too old."),
            _ => new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, version, $"Dynamis API {version} is ready."),
        };
        AvailabilityChanged?.Invoke();
    }

    public bool InspectObject(nint address) => gateway.TryInspectObject(address);
    public bool InspectRegion(nint address, nuint size) => gateway.TryInspectRegion(address, size);
    public string? GetClassName(nint address) => gateway.TryGetClassName(address);
    public bool IsInstanceOf(nint address, string className) => gateway.TryIsInstanceOf(address, className);
    public bool DrawPointer(string label, nint address) => gateway.TryDrawPointer(label, address);

    public void Dispose()
    {
        initializedSubscription.Dispose();
        disposingSubscription.Dispose();
        gateway.Dispose();
    }
}
