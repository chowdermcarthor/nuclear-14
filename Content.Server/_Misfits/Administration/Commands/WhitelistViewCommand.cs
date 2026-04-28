// #Misfits Change - Console command to open the Whitelist Viewer admin panel
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>
/// Opens the whitelist viewer so admins can see all CKEYs on the server whitelist.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class WhitelistViewCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "whitelistview";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var ui = new WhitelistViewEui();
        _eui.OpenEui(ui, player);
    }
}
