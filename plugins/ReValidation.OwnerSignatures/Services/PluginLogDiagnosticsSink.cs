using Dalamud.Plugin.Services;
using ReValidation.Common.Abstractions;

namespace ReValidation.OwnerSignatures.Services;

public sealed class PluginLogDiagnosticsSink(IPluginLog pluginLog) : IValidationDiagnosticsSink
{
    private readonly IPluginLog pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));

    public void Debug(string message) => pluginLog.Debug(message);
}
