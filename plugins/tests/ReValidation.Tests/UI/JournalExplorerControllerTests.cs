using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;
using ReValidation.DynamisBridge.UI;
using Xunit;

public sealed class JournalExplorerControllerTests
{
    [Fact]
    public void Constructor_WhenDynamisIsReady_PublishesReadyStatus()
    {
        var state = new JournalExplorerWindowState();

        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(),
            new FakePointerInspectionService());

        Assert.Equal("Ready", state.StatusText);
        Assert.Equal("Dynamis API 1.7 is ready.", state.StatusDetailText);
    }

    [Fact]
    public async Task ArmJournalSessionAsync_WhenDynamisIsUnavailable_PublishesBlockedStatus()
    {
        var state = new JournalExplorerWindowState();
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(UnavailableSnapshot()),
            new FakeJournalAnchorCollector(),
            new FakePointerInspectionService());

        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.Equal("Blocked", state.StatusText);
        Assert.Equal("Dynamis is unavailable.", state.StatusDetailText);
    }

    [Fact]
    public async Task AvailabilityChanged_FromArmedToUnavailable_ClearsSession()
    {
        var state = new JournalExplorerWindowState();
        var availability = new FakeAvailabilityService(ReadySnapshot());
        using var controller = CreateController(
            state,
            availability,
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(CreateSeed()));

        Assert.Equal("Ready", state.StatusText);
        await controller.ArmJournalSessionAsync(CancellationToken.None);
        Assert.Equal("Armed", state.StatusText);

        availability.Set(UnavailableSnapshot());

        Assert.Equal("Blocked", state.StatusText);
        Assert.Empty(state.Anchors);
        Assert.Empty(state.Candidates);
        Assert.False(state.HasActiveSession);
    }

    [Fact]
    public async Task ResetSession_WhenDynamisIsReady_ReturnsToReady()
    {
        var state = new JournalExplorerWindowState();
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(CreateSeed()));
        await controller.ArmJournalSessionAsync(CancellationToken.None);

        controller.ResetSession();

        Assert.Equal("Ready", state.StatusText);
        Assert.Empty(state.Candidates);
        Assert.False(state.HasActiveSession);
    }

    [Fact]
    public async Task ArmJournalSessionAsync_WhenReady_PopulatesRankedCandidates()
    {
        var state = new JournalExplorerWindowState();
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(CreateSeed()));

        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.Equal("Armed", state.StatusText);
        Assert.Single(state.Candidates);
        Assert.Equal("provider", state.Candidates[0].CandidateId);
        Assert.True(state.HasActiveSession);
    }

    [Fact]
    public async Task SelectedCandidateActions_ForwardObjectRegionAndPointerInspection()
    {
        var state = new JournalExplorerWindowState();
        var inspection = new FakePointerInspectionService(CreateSeed());
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            inspection);
        await controller.ArmJournalSessionAsync(CancellationToken.None);

        Assert.True(controller.InspectSelectedCandidateObject());
        Assert.True(controller.InspectSelectedCandidateRegion(0x100));
        Assert.True(controller.DrawSelectedCandidatePointer());

        Assert.Equal((nint)0x2000, inspection.LastObjectAddress);
        Assert.Equal(((nint)0x2000, (uint)0x100, "SomeProvider"), inspection.LastRegion);
        Assert.Equal(((nint)0x2000, "provider"), inspection.LastPointer);
    }

    [Fact]
    public async Task MarkCandidateDisposition_UpdatesTheSelectedCandidate()
    {
        var state = new JournalExplorerWindowState();
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(CreateSeed()));

        await controller.ArmJournalSessionAsync(CancellationToken.None);
        controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.Promising);

        Assert.Equal(JournalCandidateDisposition.Promising, Assert.Single(state.Candidates).Disposition);
    }

    [Fact]
    public async Task ExportSessionNoteAsync_WithoutActiveSession_DoesNotCreateEmptyNote()
    {
        var state = new JournalExplorerWindowState();
        using var temp = new TemporaryDirectory();
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(),
            new FakePointerInspectionService());

        await controller.ExportSessionNoteAsync(temp.Path, CancellationToken.None);

        Assert.Equal("Export Failed", state.StatusText);
        Assert.Contains("active session", state.StatusDetailText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public async Task ExportSessionNoteAsync_WithActiveSession_UsesArmTimeAndPublishesPath()
    {
        var state = new JournalExplorerWindowState();
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));
        using var temp = new TemporaryDirectory();
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(CreateSeed()),
            timeProvider);
        await controller.ArmJournalSessionAsync(CancellationToken.None);
        timeProvider.UtcNow = new DateTimeOffset(2026, 7, 28, 13, 0, 0, TimeSpan.Zero);

        await controller.ExportSessionNoteAsync(temp.Path, CancellationToken.None);

        Assert.Equal("Exported", state.StatusText);
        Assert.NotNull(state.LastExportPath);
        Assert.True(File.Exists(state.LastExportPath));
        Assert.Contains("Started: 2026-07-28T12:00:00.0000000+00:00", File.ReadAllText(state.LastExportPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportSessionNoteAsync_WhenWriterFails_PublishesFailure()
    {
        var state = new JournalExplorerWindowState();
        using var temp = new TemporaryDirectory();
        var filePath = Path.Combine(temp.Path, "not-a-directory");
        await File.WriteAllTextAsync(filePath, "occupied");
        using var controller = CreateController(
            state,
            new FakeAvailabilityService(ReadySnapshot()),
            new FakeJournalAnchorCollector(new JournalAnchorRecord("addon", (nint)0x1000, "GameGui", "UI root")),
            new FakePointerInspectionService(CreateSeed()));
        await controller.ArmJournalSessionAsync(CancellationToken.None);

        await controller.ExportSessionNoteAsync(filePath, CancellationToken.None);

        Assert.Equal("Export Failed", state.StatusText);
        Assert.Contains("could not be written", state.StatusDetailText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.LastExportPath);
    }

    private static JournalExplorerController CreateController(
        JournalExplorerWindowState state,
        IDynamisAvailabilityService availabilityService,
        IJournalAnchorCollector anchorCollector,
        IPointerInspectionService inspectionService,
        TimeProvider? timeProvider = null) =>
        new(
            state,
            availabilityService,
            anchorCollector,
            inspectionService,
            new JournalCandidateRanker(),
            new EvidenceNoteWriter(timeProvider ?? TimeProvider.System),
            "ReValidation.DynamisBridge/0.1.0",
            () => "ffxiv_dx11.exe sha256=example",
            timeProvider);

    private static DynamisAvailabilitySnapshot ReadySnapshot() =>
        new(BridgeAvailabilityStatus.Ready, new DynamisApiVersion(1, 7, 0), "Dynamis API 1.7 is ready.");

    private static DynamisAvailabilitySnapshot UnavailableSnapshot() =>
        new(BridgeAvailabilityStatus.Unavailable, null, "Dynamis is unavailable.");

    private static JournalCandidateSeed CreateSeed() =>
        new("provider", (nint)0x2000, "addon", "provider", "SomeProvider", "size=0x80", null, false, 4);

    private sealed class FakeAvailabilityService(DynamisAvailabilitySnapshot snapshot) : IDynamisAvailabilityService
    {
        public event Action? AvailabilityChanged;
        public DynamisAvailabilitySnapshot Current { get; private set; } = snapshot;
        public bool IsReady => Current.Status is BridgeAvailabilityStatus.Ready or BridgeAvailabilityStatus.SessionActive;

        public void Set(DynamisAvailabilitySnapshot value)
        {
            Current = value;
            AvailabilityChanged?.Invoke();
        }
    }

    private sealed class FakeJournalAnchorCollector(params JournalAnchorRecord[] anchors) : IJournalAnchorCollector
    {
        public IReadOnlyList<JournalAnchorRecord> CaptureAnchors() => anchors;
    }

    private sealed class FakePointerInspectionService(params JournalCandidateSeed[] seeds) : IPointerInspectionService
    {
        public nint? LastObjectAddress { get; private set; }
        public (nint Address, uint Size, string TypeName)? LastRegion { get; private set; }
        public (nint Address, string? Name)? LastPointer { get; private set; }

        public IReadOnlyList<JournalCandidateSeed> ExpandCandidates(IReadOnlyList<JournalAnchorRecord> anchors) => seeds;

        public bool InspectObject(nint address, string? name)
        {
            LastObjectAddress = address;
            return true;
        }

        public bool InspectRegion(nint address, uint size, string typeName, string? name)
        {
            LastRegion = (address, size, typeName);
            return true;
        }

        public bool DrawPointer(nint address, string? name)
        {
            LastPointer = (address, name);
            return true;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
