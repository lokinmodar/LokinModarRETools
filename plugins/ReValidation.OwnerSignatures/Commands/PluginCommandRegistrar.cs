using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ReValidation.OwnerSignatures.Windows;

namespace ReValidation.OwnerSignatures.Commands;

public sealed class PluginCommandRegistrar : IDisposable
{
    private const string Command = "/revalidate-owner";
    private readonly ICommandManager commandManager;

    public PluginCommandRegistrar(ICommandManager commandManager, ValidationWindow window)
    {
        ArgumentNullException.ThrowIfNull(commandManager);
        ArgumentNullException.ThrowIfNull(window);

        this.commandManager = commandManager;
        commandManager.AddHandler(Command, new CommandInfo((_, _) => window.IsOpen = true)
        {
            HelpMessage = "Open the owner-signature revalidation window.",
        });
    }

    public void Dispose() => commandManager.RemoveHandler(Command);
}
