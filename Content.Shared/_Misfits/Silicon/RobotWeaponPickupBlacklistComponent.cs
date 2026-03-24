using Content.Shared.Whitelist;

namespace Content.Shared._Misfits.Silicon;

/// <summary>
/// Prevents player-controlled robot chassis from picking up items that match the configured blacklist.
/// </summary>
[RegisterComponent]
public sealed partial class RobotWeaponPickupBlacklistComponent : Component
{
    /// <summary>
    /// Pickup attempts against entities matching this blacklist are cancelled.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}