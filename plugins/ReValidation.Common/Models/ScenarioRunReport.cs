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

    public static ScenarioRunReport Started(ValidationScenarioDefinition definition, ValidationRoute route, ValidationMode mode) =>
        new(definition, route, mode, null, null, null, null, null, null, [], false, null, "validate", null);

    public ScenarioRunReport WithPrecondition(ScenarioPreconditionResult precondition) =>
        this with { Precondition = precondition, CurrentPhase = "capture" };

    public ScenarioRunReport WithCapture(ScenarioCapture capture) =>
        this with { Capture = capture, CurrentPhase = "compare" };

    public ScenarioRunReport WithCompare(ScenarioCompareResult? compare) =>
        this with { CompareResult = compare, CurrentPhase = "override" };

    public ScenarioRunReport WithOverride(ScenarioOverrideTicket? ticket) =>
        this with { OverrideResult = ticket, CurrentPhase = "assert" };

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

    public ScenarioRunReport MarkFromAssert(ScenarioAssertResult? assert) =>
        assert is null || assert.Passed
            ? MarkSuccess()
            : this with { IsSuccess = false, FailedPhase = "assert", CurrentPhase = "restore" };

    public ScenarioRunReport MarkFailure(string phase, Exception exception) =>
        this with { IsSuccess = false, FailedPhase = phase, CurrentPhase = "restore", Exception = exception };

    public ScenarioRunReport MergeRestoreOutcome(ScenarioRestoreResult restore) =>
        restore.Passed || !IsSuccess
            ? this
            : this with { IsSuccess = false, FailedPhase = "restore" };
}
