using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Models;

namespace ReValidation.Common.Execution;

public sealed class ValidationScenarioRunner : IValidationScenarioRunner
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan cleanupTimeout;
    private readonly IValidationDiagnosticsSink diagnosticsSink;
    private readonly IReadOnlyList<IEvidenceWriter> evidenceWriters;
    private readonly IRouteMetadataProvider routeMetadataProvider;

    public ValidationScenarioRunner(IRouteMetadataProvider routeMetadataProvider, params IEvidenceWriter[] evidenceWriters)
        : this(routeMetadataProvider, NullValidationDiagnosticsSink.Instance, CleanupTimeout, evidenceWriters)
    {
    }

    public ValidationScenarioRunner(IRouteMetadataProvider routeMetadataProvider, IValidationDiagnosticsSink diagnosticsSink, params IEvidenceWriter[] evidenceWriters)
        : this(routeMetadataProvider, diagnosticsSink, CleanupTimeout, evidenceWriters)
    {
    }

    public ValidationScenarioRunner(IRouteMetadataProvider routeMetadataProvider, TimeSpan cleanupTimeout, params IEvidenceWriter[] evidenceWriters)
        : this(routeMetadataProvider, NullValidationDiagnosticsSink.Instance, cleanupTimeout, evidenceWriters)
    {
    }

    public ValidationScenarioRunner(
        IRouteMetadataProvider routeMetadataProvider,
        IValidationDiagnosticsSink diagnosticsSink,
        TimeSpan cleanupTimeout,
        params IEvidenceWriter[] evidenceWriters)
    {
        ArgumentNullException.ThrowIfNull(routeMetadataProvider);
        ArgumentNullException.ThrowIfNull(diagnosticsSink);
        if (cleanupTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cleanupTimeout));

        ArgumentNullException.ThrowIfNull(evidenceWriters);
        this.routeMetadataProvider = routeMetadataProvider;
        this.diagnosticsSink = diagnosticsSink;
        this.cleanupTimeout = cleanupTimeout;
        this.evidenceWriters = evidenceWriters.ToArray();
    }

    public async Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(context);

        diagnosticsSink.Debug($"run.start scenario={scenario.Definition.Id} route={context.Route} mode={context.Mode}");
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
            {
                diagnosticsSink.Debug($"run.blocked scenario={scenario.Definition.Id} phase=validate reason=\"{Escape(report.Precondition?.BlockingReason)}\"");
                return await ExportAsync(report.MarkBlocked(), context);
            }

            capture = await scenario.CaptureAsync(context, cancellationToken);
            report = report.WithCapture(capture).MarkFromCapture(capture);
            diagnosticsSink.Debug($"capture.summary scenario={scenario.Definition.Id} summary=\"{Escape(capture.Summary)}\"");

            if (report.FailedPhase is "capture")
                return await ExportAsyncAndLogAsync(report, context);

            if (context.Mode is ValidationMode.CaptureOnly)
                return await ExportAsyncAndLogAsync(report.MarkSuccess(), context);

            var compare = await scenario.CompareAsync(context, capture, cancellationToken);
            report = report.WithCompare(compare).MarkFromCompare(compare);
            diagnosticsSink.Debug($"compare.result scenario={scenario.Definition.Id} isMatch={compare?.IsMatch.ToString() ?? "null"} summary=\"{Escape(compare?.Summary)}\"");

            if (context.Mode is ValidationMode.Compare || report.FailedPhase is "compare")
                return await ExportAsyncAndLogAsync(report.FailedPhase is null ? report.MarkSuccess() : report, context);

            overrideAttempted = true;
            report = report.MarkOverrideAttempted();
            ticket = await scenario.OverrideAsync(context, capture, cancellationToken);
            report = report.WithOverride(ticket);
            diagnosticsSink.Debug($"override.result scenario={scenario.Definition.Id} ticketCreated={(ticket is not null)} summary=\"{Escape(ticket?.Summary)}\"");

            var assert = await scenario.AssertAsync(context, capture, ticket, cancellationToken);
            report = report.WithAssert(assert).MarkFromAssert(assert);
            diagnosticsSink.Debug($"assert.result scenario={scenario.Definition.Id} passed={assert?.Passed.ToString() ?? "null"} summary=\"{Escape(assert?.Summary)}\"");
        }
        catch (Exception ex)
        {
            report = report.MarkFailure(report.CurrentPhase, ex);
            diagnosticsSink.Debug($"run.exception scenario={scenario.Definition.Id} phase={report.FailedPhase ?? report.CurrentPhase} type={ex.GetType().Name} message=\"{Escape(ex.Message)}\"");
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
                    diagnosticsSink.Debug($"restore.result scenario={scenario.Definition.Id} passed={restore.Passed} summary=\"{Escape(restore.Summary)}\"");
                }
                catch (Exception ex)
                {
                    var restore = new ScenarioRestoreResult(false, "Restore failed", [ex.Message]);
                    report = report.WithRestore(restore).MarkFailure("restore", ex);
                    diagnosticsSink.Debug($"restore.exception scenario={scenario.Definition.Id} type={ex.GetType().Name} message=\"{Escape(ex.Message)}\"");
                }
            }
        }

        return await ExportAsyncAndLogAsync(report, context);
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

    private async Task<ScenarioRunReport> ExportAsyncAndLogAsync(ScenarioRunReport report, ScenarioExecutionContext context)
    {
        var exported = await ExportAsync(report, context);
        diagnosticsSink.Debug($"run.completed scenario={exported.Definition.Id} status={exported.Status} failedPhase={exported.FailedPhase ?? "none"} summary=\"{Escape(exported.Summary)}\" artifacts={exported.ArtifactPaths.Count}");
        return exported;
    }

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
