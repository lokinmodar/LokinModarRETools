using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Models;

namespace ReValidation.Common.Execution;

public sealed class ValidationScenarioRunner : IValidationScenarioRunner
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan cleanupTimeout;
    private readonly IReadOnlyList<IEvidenceWriter> evidenceWriters;
    private readonly IRouteMetadataProvider routeMetadataProvider;

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
        this.routeMetadataProvider = routeMetadataProvider;
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
        var overrideAttempted = false;

        try
        {
            if (routeMetadataProvider.Route != context.Route)
                throw new InvalidOperationException($"Route metadata provider '{routeMetadataProvider.Route}' cannot run route '{context.Route}'.");

            report = report.WithRouteMetadata(await routeMetadataProvider.GetMetadataAsync(cancellationToken));
            report = report.WithPrecondition(await scenario.ValidateAsync(context, cancellationToken));
            if (!report.CanRun)
                return await ExportAsync(report.MarkBlocked(), context);

            capture = await scenario.CaptureAsync(context, cancellationToken);
            report = report.WithCapture(capture);

            if (context.Mode is ValidationMode.CaptureOnly)
                return await ExportAsync(report.MarkSuccess(), context);

            var compare = await scenario.CompareAsync(context, capture, cancellationToken);
            report = report.WithCompare(compare).MarkFromCompare(compare);

            if (context.Mode is ValidationMode.Compare || report.FailedPhase is "compare")
                return await ExportAsync(report.FailedPhase is null ? report.MarkSuccess() : report, context);

            overrideAttempted = true;
            report = report.MarkOverrideAttempted();
            ticket = await scenario.OverrideAsync(context, capture, cancellationToken);
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
            if (overrideAttempted && capture is not null)
            {
                using var cleanupCancellationSource = new CancellationTokenSource();
                if (cleanupTimeout == TimeSpan.Zero)
                    cleanupCancellationSource.Cancel();
                else
                    cleanupCancellationSource.CancelAfter(cleanupTimeout);

                try
                {
                    var restore = await scenario.RestoreAsync(context, capture, ticket, cleanupCancellationSource.Token)
                        .AsTask()
                        .WaitAsync(cleanupCancellationSource.Token);
                    report = report.WithRestore(restore).MergeRestoreOutcome(restore);
                }
                catch (Exception ex)
                {
                    var restore = new ScenarioRestoreResult(false, "Restore failed", [ex.Message]);
                    report = report.WithRestore(restore).MarkFailure("restore", ex);
                }
            }
        }

        return await ExportAsync(report, context);
    }

    private async Task<ScenarioRunReport> ExportAsync(ScenarioRunReport report, ScenarioExecutionContext context)
    {
        foreach (var evidenceWriter in evidenceWriters)
        {
            try
            {
                var evidence = await evidenceWriter.WriteAsync(report, context, CancellationToken.None);
                report = report.WithEvidence(evidence);
            }
            catch (Exception)
            {
                report = report
                    .WithEvidence(EvidenceWriteResult.Failed(evidenceWriter.Kind))
                    .MarkExportFailure();
            }
        }

        return report;
    }
}
