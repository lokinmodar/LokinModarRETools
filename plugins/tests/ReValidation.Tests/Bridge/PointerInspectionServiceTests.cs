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
        public DynamisAvailabilitySnapshot Current { get; } = new(BridgeAvailabilityStatus.Ready, new DynamisApiVersion(1, 7, 0), "Dynamis API 1.7 is ready.");
        public void Refresh() => AvailabilityChanged?.Invoke();
        public bool InspectObject(nint address, object? @class = null, string? name = null) => true;
        public bool InspectRegion(nint address, uint size, string typeName, uint typeTemplateId = 0, uint classKindId = 0, string? name = null) => true;
        public (string Name, Type? Type, uint Size, uint Displacement)? GetClass(nint pointer) => ("AtkUnitBase", null, 0, 0);
        public (bool IsInstance, uint Displacement)? IsInstanceOf(nint pointer, string? className, Type? type) => null;
        public bool DrawPointer(nint pointer, Func<object?>? @class, Func<string?>? name, string? customText, ulong flags, System.Numerics.Vector2 size) => true;
        public void Dispose() { }
    }
}
