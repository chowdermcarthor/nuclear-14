using Content.Shared.Whitelist;

namespace Content.Server._Misfits.NPC.Components;

/// <summary>
/// Lets specific NPCs keep using hands for utility items while refusing pickup targets that match the blacklist.
/// </summary>
[RegisterComponent]
public sealed partial class NpcPickupBlacklistComponent : Component
{
    /// <summary>
    /// Entities matching this blacklist cannot be picked up by the NPC.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}