using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class DynamisAvailabilityServiceTests
{
    [Fact]
    public void Snapshot_WhenClientIsIncompatible_UsesBlockedStatusText()
    {
        var client = new StubDynamisApiClient(
            new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Incompatible, 3, "Dynamis API 3 is too old."));
        var service = new DynamisAvailabilityService(client);

        Assert.False(service.IsReady);
        Assert.Equal("Dynamis API 3 is too old.", service.Current.StatusText);
    }

    private sealed class StubDynamisApiClient(DynamisAvailabilitySnapshot snapshot) : IDynamisApiClient
    {
        public event Action? AvailabilityChanged;
        public DynamisAvailabilitySnapshot Current { get; private set; } = snapshot;
        public void Refresh() => AvailabilityChanged?.Invoke();
        public bool InspectObject(nint address) => true;
        public bool InspectRegion(nint address, nuint size) => true;
        public string? GetClassName(nint address) => null;
        public bool IsInstanceOf(nint address, string className) => false;
        public bool DrawPointer(string label, nint address) => true;
        public void Dispose() { }
    }
}
