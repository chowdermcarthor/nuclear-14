// #Misfits Change
// Alias: ./admin → same as asay (admin chat)
using Content.Server.Chat.Managers;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Chat.Commands;

[AdminCommand(AdminFlags.Adminchat)]
internal sealed class DotAdminCommand : IConsoleCommand
{
    public string Command     => ".admin";
    public string Description => "Alias for 'asay'. Send chat messages to the private admin chat channel.";
    public string Help        => ".admin <text>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command cannot be run from the server.");
            return;
        }

        if (args.Length < 1)
            return;

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        IoCManager.Resolve<IChatManager>().TrySendOOCMessage(player, message, OOCChatType.Admin);
    }
}
