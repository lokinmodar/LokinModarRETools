using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.Common.Scenarios;

public sealed class NotConfiguredValidationScenario(ValidationScenarioDefinition definition, string blockingReason) : IValidationScenario
{
    public ValidationScenarioDefinition Definition { get; } = definition;

    public ValueTask<ScenarioPreconditionResult> ValidateAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ScenarioPreconditionResult(false, blockingReason));

    public ValueTask<ScenarioCapture> CaptureAsync(ScenarioExecutionContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ScenarioCompareResult?> CompareAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ScenarioOverrideTicket?> OverrideAsync(ScenarioExecutionContext context, ScenarioCapture capture, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ScenarioAssertResult?> AssertAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
    public ValueTask<ScenarioRestoreResult> RestoreAsync(ScenarioExecutionContext context, ScenarioCapture capture, ScenarioOverrideTicket? ticket, CancellationToken cancellationToken) => throw new NotSupportedException();
}
