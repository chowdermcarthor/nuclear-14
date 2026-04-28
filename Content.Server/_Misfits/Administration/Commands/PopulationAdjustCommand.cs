// #Misfits Add - Live-adjust server population cap without restart.
// Lidgren's NetPeerConfiguration.MaximumConnections setter throws after the NetPeer is started,
// but the underlying private field m_maximumConnections is read directly on every connect handshake
// (NetPeer.Internal.cs Connect message handler). Writing it via reflection takes effect immediately.
// Also updates the relevant cvars (net.max_connections, game.maxplayers, game.soft_max_players)
// so any code path that re-queries them agrees, and so a future reboot keeps the new cap.
//
// NOTE: This does NOT update the whitelist prototype MaximumPlayers value. If the active
// whitelist (basicWhitelist or PlayerConnectionConfigurationCorvax) caps players at a lower
// number, you must hot-load a modified prototype and toggle whitelist.prototype_list to refresh
// the ConnectionManager whitelist cache. See repo memory: player-cap-whitelist-split.md.
using System.Reflection;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Server._Misfits.Administration.Commands;

[AdminCommand(AdminFlags.Server)]
public sealed class PopulationAdjustCommand : IConsoleCommand
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public string Command => "populationadjust";
    public string Description => "Live-adjusts the maximum allowed player connections without a restart. Also updates net.max_connections, game.maxplayers, and game.soft_max_players cvars.";
    public string Help => "populationadjust <newCap>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Usage: populationadjust <newCap>");
            return;
        }

        if (!int.TryParse(args[0], out var newCap) || newCap <= 0)
        {
            shell.WriteError("newCap must be a positive integer.");
            return;
        }

        // Patch the running NetPeer(s) directly. INetManager is the Robust.Shared.Network.NetManager
        // sealed class; both the _netPeers list and the NetPeerData.Peer field are private, so we
        // reflect into them. The Lidgren NetPeerConfiguration.m_maximumConnections field is the
        // value the connection-handshake check reads each time.
        var netManagerType = _net.GetType();
        var peersField = netManagerType.GetField("_netPeers", BindingFlags.Instance | BindingFlags.NonPublic);
        if (peersField?.GetValue(_net) is not System.Collections.IEnumerable peers)
        {
            shell.WriteError("Could not access NetManager._netPeers via reflection. Engine may have changed.");
            return;
        }

        // Compute a Lidgren/net.max_connections value that leaves room for whitelisted/admin
        // handshakes so privileged joins aren't rejected at the low-level network layer.
        var whitelistReserved = _cfg.GetCVar(CCVars.WhitelistReservedSlots);
        var netReserve = 128; // keep a large runtime reserve so handshake bookkeeping never trips the cap
        var netMax = newCap + whitelistReserved + netReserve;

        var patched = 0;
        foreach (var peerData in peers)
        {
            var peerField = peerData.GetType().GetField("Peer", BindingFlags.Instance | BindingFlags.Public);
            if (peerField?.GetValue(peerData) is not Lidgren.Network.NetPeer peer)
                continue;

            var config = peer.Configuration;
            var maxField = config.GetType().GetField("m_maximumConnections", BindingFlags.Instance | BindingFlags.NonPublic);
            if (maxField == null)
            {
                shell.WriteError("Could not find Lidgren m_maximumConnections field. Engine may have changed.");
                return;
            }

            maxField.SetValue(config, netMax);
            patched++;
        }

        if (patched == 0)
        {
            shell.WriteError("No active NetPeers found to patch.");
            return;
        }

        // Sync cvars so future reads/reboots see the intended limits.
        _cfg.SetCVar("net.max_connections", netMax);
        _cfg.SetCVar("game.maxplayers", netMax);  // Use netMax (with reserve), not newCap, so the hard cap accommodates privileged joins
        _cfg.SetCVar(CCVars.SoftMaxPlayers, newCap);

        shell.WriteLine($"Population cap raised to {newCap} on {patched} NetPeer(s). net.max_connections set to {netMax} (soft + whitelist + reserve).");
        shell.WriteLine("Reminder: if the active whitelist prototype caps below this value, also hot-load an updated whitelist and toggle whitelist.prototype_list.");
    }
}
