using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using ReValidation.DynamisBridge.UI;
using Xunit;

public sealed class JournalExplorerControllerTests
{
    [Fact]
    public async Task ArmJournalSessionAsync_WhenDynamisIsUnavailable_PublishesBlockedStatus()
    {
        var state = new JournalExplorerWindowState();
        var controller = CreateController(
            state,
            new FakeAvailabilityService(new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable.")),
            new FakeJournalAnchorCollector(),
            new FakePointerInspectionService());

        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.Equal("Blocked", state.StatusText);
        Assert.Equal("Dynamis is unavailable.", state.StatusDetailText);
    }

    [Fact]
    public async Task ArmJournalSessionAsync_WhenReady_PopulatesRankedCandidates()
    {
        var state = new JournalExplorerWindowState();
        var controller = CreateController(
            state,
            new FakeAvailabilityService(new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, 4, "Dynamis API 4 is ready.")),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(new JournalCandidateSeed("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", null, false, 4)));

        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.Equal("Armed", state.StatusText);
        Assert.Single(state.Candidates);
        Assert.Equal("provider", state.Candidates[0].CandidateId);
    }

    [Fact]
    public async Task MarkCandidateDisposition_UpdatesTheSelectedCandidate()
    {
        var state = new JournalExplorerWindowState();
        var controller = CreateController(
            state,
            new FakeAvailabilityService(new DynamisAvailabilitySnapshot(BridgeAvailabilityStatus.Ready, 4, "Dynamis API 4 is ready.")),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(new JournalCandidateSeed("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", null, false, 4)));

        await controller.ArmJournalSessionAsync(CancellationToken.None);
        controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.HighValueForIda);

        Assert.Equal(JournalCandidateDisposition.HighValueForIda, Assert.Single(state.Candidates).Disposition);
    }

    private static JournalExplorerController CreateController(
        JournalExplorerWindowState state,
        IDynamisAvailabilityService availabilityService,
        IJournalAnchorCollector anchorCollector,
        IPointerInspectionService inspectionService) =>
        new(
            state,
            availabilityService,
            anchorCollector,
            inspectionService,
            new JournalCandidateRanker(),
            new EvidenceNoteWriter(TimeProvider.System),
            "ReValidation.DynamisBridge/0.1.0",
            () => "ffxiv_dx11.exe sha256=example");

    private sealed class FakeAvailabilityService(DynamisAvailabilitySnapshot snapshot) : IDynamisAvailabilityService
    {
        public DynamisAvailabilitySnapshot Current => snapshot;
        public bool IsReady => snapshot.Status is BridgeAvailabilityStatus.Ready or BridgeAvailabilityStatus.SessionActive;
    }

    private sealed class FakeJournalAnchorCollector(params JournalAnchorRecord[] anchors) : IJournalAnchorCollector
    {
        public IReadOnlyList<JournalAnchorRecord> CaptureAnchors() => anchors;
    }

    private sealed class FakePointerInspectionService(params JournalCandidateSeed[] seeds) : IPointerInspectionService
    {
        public IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors) => seeds;
        public bool InspectObject(nint address) => true;
        public bool InspectRegion(nint address, nuint size) => true;
        public bool DrawPointer(string label, nint address) => true;
    }
}
