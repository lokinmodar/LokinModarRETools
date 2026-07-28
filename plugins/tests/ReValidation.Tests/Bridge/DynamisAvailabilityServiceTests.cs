using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class DynamisAvailabilityServiceTests
{
    [Fact]
    public void Snapshot_WhenClientIsIncompatible_UsesBlockedStatusText()
    {
        var client = new StubDynamisApiClient(
            new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Incompatible, new DynamisApiVersion(1, 6, 0), "Dynamis API 1.6 is incompatible."));
        var service = new DynamisAvailabilityService(client);

        Assert.False(service.IsReady);
        Assert.Equal("Dynamis API 1.6 is incompatible.", service.Current.StatusText);
    }

    private sealed class StubDynamisApiClient(DynamisAvailabilitySnapshot snapshot) : IDynamisApiClient
    {
        public event Action? AvailabilityChanged;
        public DynamisAvailabilitySnapshot Current { get; private set; } = snapshot;
        public void Refresh() => AvailabilityChanged?.Invoke();
        public bool InspectObject(nint address, object? @class = null, string? name = null) => true;
        public bool InspectRegion(nint address, uint size, string typeName, uint typeTemplateId = 0, uint classKindId = 0, string? name = null) => true;
        public (string Name, Type? Type, uint Size, uint Displacement)? GetClass(nint pointer) => null;
        public (bool IsInstance, uint Displacement)? IsInstanceOf(nint pointer, string? className, Type? type) => null;
        public bool DrawPointer(nint pointer, Func<object?>? @class, Func<string?>? name, string? customText, ulong flags, System.Numerics.Vector2 size) => true;
        public void Dispose() { }
    }
}
