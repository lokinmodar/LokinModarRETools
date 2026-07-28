using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.Services;

namespace ReValidation.DynamisBridge.UI;

public sealed class JournalExplorerController : IDisposable
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
        availabilityService.AvailabilityChanged += OnAvailabilityChanged;
        ApplyAvailability();
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
        var startedAtUtc = timeProvider.GetUtcNow();
        var anchors = anchorCollector.CaptureAnchors();
        var seeds = inspectionService.ExpandCandidates(anchors);
        state.SetSession(startedAtUtc, anchors, ranker.Rank(seeds));
        return Task.CompletedTask;
    }

    public async Task ExportSessionNoteAsync(string outputRoot, CancellationToken cancellationToken)
    {
        if (!state.HasActiveSession || state.SessionStartedAtUtc is not { } startedAtUtc)
        {
            state.SetExportFailed("A session note requires an active session.");
            return;
        }

        var session = new JournalProbeSession(
            startedAtUtc,
            pluginVersion,
            availabilityService.Current.ApiVersion,
            executableIdentityProvider(),
            state.Anchors,
            state.Candidates);
        try
        {
            var path = await noteWriter.WriteAsync(session, outputRoot, cancellationToken);
            state.SetExport(path);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            state.SetExportFailed($"The session note could not be written: {exception.Message}");
        }
    }

    public void ResetSession() => ApplyAvailability(forceReset: true);

    public void MarkSelectedCandidateDisposition(JournalCandidateDisposition disposition) =>
        state.SetCandidateDisposition(disposition);

    public bool InspectSelectedCandidateObject()
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null && inspectionService.InspectObject(candidate.Address, candidate.CandidateId);
    }

    public bool InspectSelectedCandidateRegion(uint size)
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null
            && inspectionService.InspectRegion(candidate.Address, size, candidate.ClassName ?? "byte", candidate.CandidateId);
    }

    public bool DrawSelectedCandidatePointer()
    {
        var candidate = state.Candidates.FirstOrDefault(entry => entry.CandidateId == state.SelectedCandidateId);
        return candidate is not null && inspectionService.DrawPointer(candidate.Address, candidate.CandidateId);
    }

    public void Dispose() => availabilityService.AvailabilityChanged -= OnAvailabilityChanged;

    private void OnAvailabilityChanged() => ApplyAvailability();

    private void ApplyAvailability(bool forceReset = false)
    {
        if (!availabilityService.IsReady)
        {
            state.SetBlocked(availabilityService.Current.StatusText);
            return;
        }

        if (forceReset)
            state.Reset(availabilityService.Current.StatusText);
        else if (!state.HasActiveSession)
            state.SetReady(availabilityService.Current.StatusText);
    }
}
