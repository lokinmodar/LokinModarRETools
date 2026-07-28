using System.Numerics;
using ReValidation.DynamisBridge.Ipc;
using ReValidation.DynamisBridge.Models;

namespace ReValidation.DynamisBridge.Services;

public interface IDynamisApiClient : IDisposable
{
    event Action? AvailabilityChanged;
    DynamisAvailabilitySnapshot Current { get; }
    void Refresh();
    bool InspectObject(nint address, object? @class = null, string? name = null);
    bool InspectRegion(nint address, uint size, string typeName, uint typeTemplateId = 0, uint classKindId = 0, string? name = null);
    (string Name, Type? Type, uint Size, uint Displacement)? GetClass(nint pointer);
    (bool IsInstance, uint Displacement)? IsInstanceOf(nint pointer, string? className, Type? type);
    bool DrawPointer(nint pointer, Func<object?>? @class, Func<string?>? name, string? customText, ulong flags, Vector2 size);
}

public interface IDynamisAvailabilityService
{
    event Action? AvailabilityChanged;
    DynamisAvailabilitySnapshot Current { get; }
    bool IsReady { get; }
}

public sealed class DynamisApiClient : IDynamisApiClient
{
    private readonly IDynamisIpcGateway gateway;
    private readonly uint requiredMajorVersion;
    private readonly uint minimumMinorVersion;
    private readonly IDisposable initializedSubscription;
    private readonly IDisposable disposingSubscription;

    public DynamisApiClient(IDynamisIpcGateway gateway, uint requiredMajorVersion, uint minimumMinorVersion)
    {
        this.gateway = gateway;
        this.requiredMajorVersion = requiredMajorVersion;
        this.minimumMinorVersion = minimumMinorVersion;
        initializedSubscription = gateway.SubscribeApiInitialized(PublishInitialized);
        disposingSubscription = gateway.SubscribeApiDisposing(PublishUnavailable);
        Current = UnavailableSnapshot();
    }

    public event Action? AvailabilityChanged;
    public DynamisAvailabilitySnapshot Current { get; private set; }

    public void Refresh()
    {
        var version = gateway.TryGetApiVersion();
        if (version is null)
        {
            PublishUnavailable();
            return;
        }

        PublishVersion(new DynamisApiVersion(version.Value.MajorVersion, version.Value.MinorVersion, version.Value.FeatureFlags));
    }

    private void PublishInitialized(uint majorVersion, uint minorVersion, ulong featureFlags, Version pluginVersion)
    {
        _ = pluginVersion;
        PublishVersion(new DynamisApiVersion(majorVersion, minorVersion, featureFlags));
    }

    private void PublishVersion(DynamisApiVersion version)
    {
        Current = version.MajorVersion == requiredMajorVersion && version.MinorVersion >= minimumMinorVersion
            ? new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, version, $"Dynamis API {version} is ready.")
            : new DynamisAvailabilitySnapshot(
                BridgeAvailabilityStatus.Incompatible,
                version,
                $"Dynamis API {version} is incompatible; API {requiredMajorVersion}.{minimumMinorVersion} or newer within major {requiredMajorVersion} is required.");
        AvailabilityChanged?.Invoke();
    }

    private void PublishUnavailable()
    {
        Current = UnavailableSnapshot();
        AvailabilityChanged?.Invoke();
    }

    private static DynamisAvailabilitySnapshot UnavailableSnapshot() =>
        new(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable.");

    public bool InspectObject(nint address, object? @class = null, string? name = null) =>
        gateway.TryInspectObject(address, @class, name);

    public bool InspectRegion(
        nint address,
        uint size,
        string typeName,
        uint typeTemplateId = 0,
        uint classKindId = 0,
        string? name = null) =>
        gateway.TryInspectRegion(address, size, typeName, typeTemplateId, classKindId, name);

    public (string Name, Type? Type, uint Size, uint Displacement)? GetClass(nint pointer) =>
        gateway.TryGetClass(pointer);

    public (bool IsInstance, uint Displacement)? IsInstanceOf(nint pointer, string? className, Type? type) =>
        gateway.TryIsInstanceOf(pointer, className, type);

    public bool DrawPointer(
        nint pointer,
        Func<object?>? @class,
        Func<string?>? name,
        string? customText,
        ulong flags,
        Vector2 size) =>
        gateway.TryDrawPointer(pointer, @class, name, customText, flags, size);

    public void Dispose()
    {
        initializedSubscription.Dispose();
        disposingSubscription.Dispose();
        gateway.Dispose();
    }
}
