using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ReValidation.DynamisBridge.Windows;

namespace ReValidation.DynamisBridge.Commands;

public sealed class PluginCommandRegistrar : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly JournalExplorerWindow window;

    public PluginCommandRegistrar(ICommandManager commandManager, JournalExplorerWindow window)
    {
        this.commandManager = commandManager;
        this.window = window;
        commandManager.AddHandler("/revalidation-dynamis", new CommandInfo((_, _) => window.IsOpen = true)
        {
            HelpMessage = "Open the Dynamis bridge window.",
        });
    }

    public void Dispose()
    {
        commandManager.RemoveHandler("/revalidation-dynamis");
    }
}
