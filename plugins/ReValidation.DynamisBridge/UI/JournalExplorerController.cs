using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;

namespace ReValidation.DynamisBridge.UI;

public sealed class JournalExplorerController
{
    private readonly JournalExplorerWindowState state;
    private readonly IDynamisAvailabilityService availabilityService;
    private readonly IJournalAnchorCollector anchorCollector;
    private readonly IPointerInspectionService inspectionService;
    private readonly JournalCandidateRanker ranker;
    private readonly EvidenceNoteWriter noteWriter;
    private readonly string pluginVersion;
    private readonly Func<string?> executableIdentityProvider;
    private readonly TimeProvider timeProvider;

    public JournalExplorerController(
        JournalExplorerWindowState state,
        IDynamisAvailabilityService availabilityService,
        IJournalAnchorCollector anchorCollector,
        IPointerInspectionService inspectionService,
        JournalCandidateRanker ranker,
        EvidenceNoteWriter noteWriter,
        string pluginVersion,
        Func<string?> executableIdentityProvider,
        TimeProvider? timeProvider = null)
    {
        this.state = state;
        this.availabilityService = availabilityService;
        this.anchorCollector = anchorCollector;
        this.inspectionService = inspectionService;
        this.ranker = ranker;
        this.noteWriter = noteWriter;
        this.pluginVersion = pluginVersion;
        this.executableIdentityProvider = executableIdentityProvider;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public JournalExplorerWindowState State => state;

    public Task ArmJournalSessionAsync(CancellationToken cancellationToken)
    {
        if (!availabilityService.IsReady)
        {
            state.SetBlocked(availabilityService.Current.StatusText);
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var anchors = anchorCollector.CaptureAnchors();
        var seeds = inspectionService.ExpandCandidates(anchors);
        state.SetSession(anchors, ranker.Rank(seeds));
        return Task.CompletedTask;
    }

    public async Task ExportSessionNoteAsync(string outputRoot, CancellationToken cancellationToken)
    {
        var session = new JournalProbeSession(
            timeProvider.GetUtcNow(),
            pluginVersion,
            availabilityService.Current.ApiVersion,
            executableIdentityProvider(),
            state.Anchors,
            state.Candidates);
        var path = await noteWriter.WriteAsync(session, outputRoot, cancellationToken);
        state.SetExport(path);
    }

    public void MarkSelectedCandidateDisposition(JournalCandidateDisposition disposition) =>
        state.SetCandidateDisposition(disposition);

    public bool InspectSelectedCandidateObject()
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null && inspectionService.InspectObject(candidate.Address);
    }

    public bool InspectSelectedCandidateRegion(nuint size)
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null && inspectionService.InspectRegion(candidate.Address, size);
    }
}
