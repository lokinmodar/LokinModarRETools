using ReValidation.Common.Evidence;

namespace ReValidation.Common.Models;

public sealed record ScenarioRunReport(
    ValidationScenarioDefinition Definition,
    ValidationRoute Route,
    ValidationMode Mode,
    ScenarioPreconditionResult? Precondition,
    ScenarioCapture? Capture,
    ScenarioCompareResult? CompareResult,
    ScenarioOverrideTicket? OverrideResult,
    ScenarioAssertResult? AssertResult,
    ScenarioRestoreResult? RestoreResult,
    IReadOnlyList<EvidenceWriteResult> Evidence,
    bool IsSuccess,
    string? FailedPhase,
    string CurrentPhase,
    Exception? Exception)
{
    public bool CanRun => Precondition?.CanRun ?? false;
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public string EvidenceRunId { get; } = Guid.NewGuid().ToString("N");
    public IReadOnlyDictionary<string, string?> RouteMetadata { get; init; } = new Dictionary<string, string?>();
    public bool OverrideAttempted { get; init; }
    public ValidationScenarioDefinition Scenario => Definition;
    public IReadOnlyList<string> ArtifactPaths => Evidence
        .Where(evidence => evidence.IsSuccess && !string.IsNullOrWhiteSpace(evidence.OutputPath))
        .Select(evidence => evidence.OutputPath)
        .ToArray();
    public string Status => IsSuccess ? "success" : FailedPhase is null ? "incomplete" : "failed";
    public string Summary => Exception?.Message
        ?? AssertResult?.Summary
        ?? CompareResult?.Summary
        ?? Capture?.FailureReason
        ?? Capture?.Summary
        ?? Precondition?.BlockingReason
        ?? Status;

    public static ScenarioRunReport Started(ValidationScenarioDefinition definition, ValidationRoute route, ValidationMode mode) =>
        new(definition, route, mode, null, null, null, null, null, null, [], false, null, "validate", null);

    public static ScenarioRunReport CreateForTests(string scenarioId, ValidationRoute route, ValidationMode mode) =>
        Started(new ValidationScenarioDefinition(scenarioId, scenarioId), route, mode).MarkSuccess();

    public ScenarioRunReport WithRouteMetadata(IReadOnlyDictionary<string, string?> routeMetadata) =>
        this with
        {
            RouteMetadata = new Dictionary<string, string?>(routeMetadata, StringComparer.Ordinal),
            CurrentPhase = "validate",
        };

    public ScenarioRunReport WithPrecondition(ScenarioPreconditionResult precondition) =>
        this with { Precondition = precondition, CurrentPhase = "capture" };

    public ScenarioRunReport WithCapture(ScenarioCapture capture) =>
        this with { Capture = capture, CurrentPhase = "compare" };

    public ScenarioRunReport MarkFromCapture(ScenarioCapture capture) =>
        capture.Passed
            ? this
            : this with { IsSuccess = false, FailedPhase = "capture", CurrentPhase = "export" };

    public ScenarioRunReport WithCompare(ScenarioCompareResult? compare) =>
        this with { CompareResult = compare, CurrentPhase = "override" };

    public ScenarioRunReport WithOverride(ScenarioOverrideTicket? ticket) =>
        this with { OverrideResult = ticket, CurrentPhase = "assert" };

    public ScenarioRunReport MarkOverrideAttempted() =>
        this with { OverrideAttempted = true, CurrentPhase = "override" };

    public ScenarioRunReport WithAssert(ScenarioAssertResult? assert) =>
        this with { AssertResult = assert, CurrentPhase = "restore" };

    public ScenarioRunReport WithRestore(ScenarioRestoreResult restore) =>
        this with { RestoreResult = restore, CurrentPhase = "export" };

    public ScenarioRunReport WithEvidence(EvidenceWriteResult evidence) =>
        this with { Evidence = [.. Evidence, evidence], CurrentPhase = "export" };

    public ScenarioRunReport MarkBlocked() =>
        this with { IsSuccess = false, FailedPhase = "validate", CurrentPhase = "export" };

    public ScenarioRunReport MarkSuccess() =>
        this with { IsSuccess = true, CurrentPhase = "export" };

    public ScenarioRunReport MarkFromCompare(ScenarioCompareResult? compare) =>
        compare is { IsMatch: true }
            ? this
            : this with { IsSuccess = false, FailedPhase = "compare", CurrentPhase = "override" };

    public ScenarioRunReport MarkFromAssert(ScenarioAssertResult? assert) =>
        assert is { Passed: true } && FailedPhase is null
            ? MarkSuccess()
            : this with { IsSuccess = false, FailedPhase = FailedPhase ?? "assert", CurrentPhase = "restore" };

    public ScenarioRunReport MarkFailure(string phase, Exception exception) =>
        this with { IsSuccess = false, FailedPhase = phase, CurrentPhase = "restore", Exception = exception };

    public ScenarioRunReport MarkExportFailure() =>
        this with { IsSuccess = false, FailedPhase = FailedPhase ?? "export", CurrentPhase = "export" };

    public ScenarioRunReport MergeRestoreOutcome(ScenarioRestoreResult restore) =>
        restore.Passed || !IsSuccess
            ? this
            : this with { IsSuccess = false, FailedPhase = "restore" };
}
