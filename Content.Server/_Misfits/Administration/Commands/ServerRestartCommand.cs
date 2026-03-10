// #Misfits Add - Admin command to perform a clean server restart via process manager (watchdog/systemd)
// NOTE: IBaseServer.Restart() is broken in the engine ("explodes very violently").
// The correct pattern is Shutdown() — the SS14 watchdog or systemd will relaunch the process.
// If the watchdog has already staged a new build (UpdatePending), the announcement reflects that.
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.ServerUpdates;
using Content.Shared.Administration;
using Robust.Server;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>
/// Performs a full server restart by shutting down cleanly so the process manager
/// (SS14 Watchdog, systemd Restart=always, etc.) can relaunch the server.
/// If the watchdog has a staged update ready, announces a deployment restart instead.
/// </summary>
[AdminCommand(AdminFlags.Server)]
public sealed class ServerRestartCommand : IConsoleCommand
{
    [Dependency] private readonly IBaseServer _server = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ServerUpdateManager _updateManager = default!;

    public string Command => "misfitsrestart";
    public string Description => "Performs a full server restart (clean shutdown; process manager will relaunch). Announces a deployment if a build is pending.";
    public string Help => "misfitsrestart";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // If the watchdog already staged a new build, use the deployment announcement.
        // Otherwise use the generic restart announcement.
        var announcement = _updateManager.UpdatePending
            ? Loc.GetString("misfits-server-restart-announcement-update")
            : Loc.GetString("misfits-server-restart-announcement");

        _chatManager.DispatchServerAnnouncement(announcement);
        _server.Shutdown(Loc.GetString("misfits-server-restart-shutdown-reason"));
    }
}
