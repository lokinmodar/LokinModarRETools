using ReValidation.Common.Abstractions;
using ReValidation.Common.Models;

namespace ReValidation.Common.Execution;

public interface IValidationScenarioRunner
{
    Task<ScenarioRunReport> RunAsync(IValidationScenario scenario, ScenarioExecutionContext context, CancellationToken cancellationToken);
}
