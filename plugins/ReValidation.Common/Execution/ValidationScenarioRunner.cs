using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Models;

namespace ReValidation.Common.Execution;

public sealed class ValidationScenarioRunner
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan cleanupTimeout;
    private readonly IReadOnlyList<IEvidenceWriter> evidenceWriters;

    public ValidationScenarioRunner(IRouteMetadataProvider routeMetadataProvider, params IEvidenceWriter[] evidenceWriters)
        : this(routeMetadataProvider, CleanupTimeout, evidenceWriters)
    {
    }

    public ValidationScenarioRunner(IRouteMetadataProvider routeMetadataProvider, TimeSpan cleanupTimeout, params IEvidenceWriter[] evidenceWriters)
    {
        ArgumentNullException.ThrowIfNull(routeMetadataProvider);
        if (cleanupTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cleanupTimeout));

        ArgumentNullException.ThrowIfNull(evidenceWriters);
        this.cleanupTimeout = cleanupTimeout;
        this.evidenceWriters = evidenceWriters.ToArray();
    }

    public async Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(context);

        var report = ScenarioRunReport.Started(scenario.Definition, context.Route, context.Mode);
        ScenarioCapture? capture = null;
        ScenarioOverrideTicket? ticket = null;
        var overrideStarted = false;

        try
        {
            report = report.WithPrecondition(await scenario.ValidateAsync(context, cancellationToken));
            if (!report.CanRun)
                return await ExportAsync(report.MarkBlocked(), context, cancellationToken);

            capture = await scenario.CaptureAsync(context, cancellationToken);
            report = report.WithCapture(capture);

            if (context.Mode is ValidationMode.CaptureOnly)
                return await ExportAsync(report.MarkSuccess(), context, cancellationToken);

            var compare = await scenario.CompareAsync(context, capture, cancellationToken);
            report = report.WithCompare(compare).MarkFromCompare(compare);

            if (context.Mode is ValidationMode.Compare)
                return await ExportAsync(report.FailedPhase is null ? report.MarkSuccess() : report, context, cancellationToken);

            ticket = await scenario.OverrideAsync(context, capture, cancellationToken);
            overrideStarted = ticket is not null;
            report = report.WithOverride(ticket);

            var assert = await scenario.AssertAsync(context, capture, ticket, cancellationToken);
            report = report.WithAssert(assert).MarkFromAssert(assert);
        }
        catch (Exception ex)
        {
            report = report.MarkFailure(report.CurrentPhase, ex);
        }
        finally
        {
            if (overrideStarted && capture is not null)
            {
                using var cleanupCancellationSource = new CancellationTokenSource();
                if (cleanupTimeout == TimeSpan.Zero)
                    cleanupCancellationSource.Cancel();
                else
                    cleanupCancellationSource.CancelAfter(cleanupTimeout);

                try
                {
                    var restore = await scenario.RestoreAsync(context, capture, ticket, cleanupCancellationSource.Token);
                    report = report.WithRestore(restore).MergeRestoreOutcome(restore);
                }
                catch (Exception ex)
                {
                    var restore = new ScenarioRestoreResult(false, "Restore failed", [ex.Message]);
                    report = report.WithRestore(restore).MarkFailure("restore", ex);
                }
            }
        }

        return await ExportAsync(report, context, cancellationToken);
    }

    private async Task<ScenarioRunReport> ExportAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        foreach (var evidenceWriter in evidenceWriters)
        {
            var evidence = await evidenceWriter.WriteAsync(report, context, cancellationToken);
            report = report.WithEvidence(evidence);
        }

        return report;
    }
}
