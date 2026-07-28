using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using Xunit;

public sealed class PointerInspectionServiceTests
{
    [Fact]
    public void ExpandCandidates_UsesOnlyCapturedAnchors()
    {
        var anchorAddress = (nint)0x1000;
        var service = new PointerInspectionService(new FakeDynamisApiClient());

        var candidates = service.ExpandCandidates([new JournalAnchorRecord("journal-addon", anchorAddress, "GameGui", "UI root")]);

        var candidate = Assert.Single(candidates);
        Assert.Equal(anchorAddress, candidate.Address);
        Assert.Equal(0, candidate.ChildPointerCount);
    }

    private sealed class FakeDynamisApiClient : IDynamisApiClient
    {
        public event Action? AvailabilityChanged;
        public DynamisAvailabilitySnapshot Current { get; } = new(BridgeAvailabilityStatus.Ready, 4, "Dynamis API 4 is ready.");
        public void Refresh() => AvailabilityChanged?.Invoke();
        public bool InspectObject(nint address) => true;
        public bool InspectRegion(nint address, nuint size) => true;
        public string? GetClassName(nint address) => "AtkUnitBase";
        public bool IsInstanceOf(nint address, string className) => false;
        public bool DrawPointer(string label, nint address) => true;
        public void Dispose() { }
    }
}
