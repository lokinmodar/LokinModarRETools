using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace ReValidation.DynamisBridge.Services;

public sealed class PluginServices
{
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
}
