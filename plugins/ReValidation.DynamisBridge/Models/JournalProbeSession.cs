namespace ReValidation.DynamisBridge.Models;

public sealed record JournalProbeSession(
    DateTimeOffset StartedAtUtc,
    string PluginVersion,
    int? DynamisApiVersion,
    string? ExecutableIdentity,
    IReadOnlyList<JournalAnchorRecord> Anchors,
    IReadOnlyList<JournalCandidateRecord> Candidates);
