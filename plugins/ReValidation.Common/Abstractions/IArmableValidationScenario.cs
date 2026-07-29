using ReValidation.Common.Models;

namespace ReValidation.Common.Abstractions;

public interface IArmableValidationScenario
{
    string ArmPrompt { get; }
    bool RequiresArming => false;
    ValueTask ArmAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    ValueTask DisarmAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken);
}
