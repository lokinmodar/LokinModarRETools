using ReValidation.Common.Abstractions;
using ReValidation.Common.Evidence;
using ReValidation.Common.Models;

namespace ReValidation.Common.Execution;

public sealed class ValidationScenarioRunner
{
    private readonly IEvidenceWriter evidenceWriter;

    public ValidationScenarioRunner(IRouteMetadataProvider routeMetadataProvider, IEvidenceWriter evidenceWriter)
    {
        ArgumentNullException.ThrowIfNull(routeMetadataProvider);
        this.evidenceWriter = evidenceWriter ?? throw new ArgumentNullException(nameof(evidenceWriter));
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
            report = report.WithCompare(compare);

            if (context.Mode is ValidationMode.Compare)
                return await ExportAsync(report.MarkSuccess(), context, cancellationToken);

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
                var restore = await scenario.RestoreAsync(context, capture, ticket, cancellationToken);
                report = report.WithRestore(restore).MergeRestoreOutcome(restore);
            }
        }

        return await ExportAsync(report, context, cancellationToken);
    }

    private async Task<ScenarioRunReport> ExportAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var evidence = await evidenceWriter.WriteAsync(report, context, cancellationToken);
        return report.WithEvidence(evidence);
    }
}
