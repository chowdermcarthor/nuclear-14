// #Misfits Add — provides helpers for building player Filters by NPC faction membership,
// used by the admin faction-announce feature so admins can address Legion, NCR, BoS, etc. separately.

using Content.Shared.NPC.Components;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Misfits.FactionAnnounce;

/// <summary>
///     Server-side system exposing a helper that returns a <see cref="Filter"/>
///     containing every connected player whose pawn belongs to a given NPC faction.
///     Used by <c>AdminAnnounceEui</c> to deliver faction-targeted announcements.
/// </summary>
public sealed class FactionAnnounceSystem : EntitySystem
{
    /// <summary>
    ///     Builds a <see cref="Filter"/> of all connected players whose pawn has
    ///     <see cref="NpcFactionMemberComponent"/> with <paramref name="npcFactionId"/> in its faction set.
    /// </summary>
    /// <param name="npcFactionId">
    ///     The prototype ID of the NPC faction to match, e.g. "CaesarLegion", "NCR", "BrotherhoodOfSteel".
    /// </param>
    /// <returns>A filter containing only the matching players (may be empty if nobody is online as that faction).</returns>
    public Filter BuildFactionFilter(string npcFactionId)
    {
        var filter = Filter.Empty();

        // Iterate every entity that has both a faction membership and an active player session.
        var query = EntityQueryEnumerator<NpcFactionMemberComponent, ActorComponent>();
        while (query.MoveNext(out _, out var factionComp, out var actor))
        {
            // NpcFactionMemberComponent.Factions is a HashSet<ProtoId<NpcFactionPrototype>>.
            // ProtoId<T> is implicitly convertible from string, so this lookup is safe.
            foreach (var f in factionComp.Factions)
            {
                if (f.Id == npcFactionId)
                {
                    filter.AddPlayer(actor.PlayerSession);
                    break;
                }
            }
        }

        return filter;
    }
}
