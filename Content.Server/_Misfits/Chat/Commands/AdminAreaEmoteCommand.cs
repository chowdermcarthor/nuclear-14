// #Misfits Change
// Admin area emote command — sends a green italic emote to all players in local (voice) range.
using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server.Chat.Commands;

/// <summary>
///     /aemote — admin area emote.  Works like /me but is always green and ignores
///     action-blockers; visible to every player within normal voice range.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
internal sealed class AdminAreaEmoteCommand : IConsoleCommand
{
    public string Command     => "aemote";
    public string Description => "Perform a passive ambient emote to players in local range (green text). Admin-only.";
    public string Help        => "aemote <text>";


    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command cannot be run from the server console.");
            return;
        }

        if (player.Status != SessionStatus.InGame)
        {
            shell.WriteError("You must be in-game to use this command.");
            return;
        }

        if (player.AttachedEntity is not { } playerEntity)
        {
            shell.WriteError("You don't have an entity!");
            return;
        }

        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        var message = string.Join(" ", args).Trim();
        if (string.IsNullOrEmpty(message))
            return;

        IoCManager.Resolve<IEntitySystemManager>()
                  .GetEntitySystem<ChatSystem>()
                  .TrySendAdminAreaEmote(playerEntity, message, player);
    }
}
