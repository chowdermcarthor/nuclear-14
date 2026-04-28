using System.Reflection;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Server._Misfits.Administration.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class PopulationStatusCommand : IConsoleCommand
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public string Command => "populationstatus";
    public string Description => "Shows current population-related CVars and Lidgren peer maximum connection values.";
    public string Help => "populationstatus";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        try
        {
            shell.WriteLine("CVar values:");
            var soft = _cfg.GetCVar(CCVars.SoftMaxPlayers);
            var whitelist = _cfg.GetCVar("game.whitelist_reserved_slots");
            var netMax = _cfg.GetCVar("net.max_connections");
            shell.WriteLine($"  game.soft_max_players = {soft}");
            shell.WriteLine($"  game.whitelist_reserved_slots = {whitelist}");
            shell.WriteLine($"  net.max_connections = {netMax}");

            shell.WriteLine("\nInspecting active NetPeers (via reflection):");
            var netManagerType = _net.GetType();
            var peersField = netManagerType.GetField("_netPeers", BindingFlags.Instance | BindingFlags.NonPublic);
            if (peersField?.GetValue(_net) is not System.Collections.IEnumerable peers)
            {
                shell.WriteError("Could not access NetManager._netPeers via reflection.");
                return;
            }

            var i = 0;
            foreach (var peerData in peers)
            {
                i++;
                var peerField = peerData.GetType().GetField("Peer", BindingFlags.Instance | BindingFlags.Public);
                if (peerField?.GetValue(peerData) is not Lidgren.Network.NetPeer peer)
                {
                    shell.WriteLine($"  Peer[{i}]: (unable to read Peer field)");
                    continue;
                }

                var config = peer.Configuration;
                var maxField = config.GetType().GetField("m_maximumConnections", BindingFlags.Instance | BindingFlags.NonPublic);
                object? peerMax = maxField?.GetValue(config) ?? "<missing>";
                var handshakesField = peer.GetType().GetField("m_handshakes", BindingFlags.Instance | BindingFlags.NonPublic);
                var handshakesCount = handshakesField?.GetValue(peer) is System.Collections.IDictionary handshakes ? handshakes.Count : -1;

                var endpoint = peer.Socket?.LocalEndPoint?.ToString() ?? "<no-endpoint>";
                var reservedSlots = handshakesCount >= 0 ? handshakesCount + peer.ConnectionsCount : -1;
                shell.WriteLine($"  Peer[{i}]: Endpoint={endpoint} ConfigMax={peerMax} CurrentConnections={peer.ConnectionsCount} Handshakes={handshakesCount} ReservedSlots={reservedSlots}");
            }
        }
        catch (Exception e)
        {
            shell.WriteError($"populationstatus failed: {e}");
        }
    }
}
