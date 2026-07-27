using ReValidation.Common.Models;

namespace ReValidation.Common.Abstractions;

public interface IArmableValidationScenario
{
    string ArmPrompt { get; }
    ValueTask<ScenarioArmState> PollArmCueAsync(CancellationToken cancellationToken);
}
