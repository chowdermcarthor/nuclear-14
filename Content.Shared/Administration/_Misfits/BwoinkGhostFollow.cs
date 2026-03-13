// #Misfits Add — network message for ghost-follow from the Bwoink/AHelp admin panel.
// When an admin clicks "Follow" in the AHelp UI, this message is sent to the server.
// The server ensures the admin is in aghost mode before starting the orbit.
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
/// Sent by an admin client to request ghost-follow on the currently selected player in the AHelp/Bwoink UI.
/// The server will enter aghost mode if needed, then start following the target's attached entity.
/// </summary>
[Serializable, NetSerializable]
public sealed class BwoinkAdminGhostFollowMessage : EntityEventArgs
{
    public NetUserId TargetUserId { get; }

    public BwoinkAdminGhostFollowMessage(NetUserId targetUserId)
    {
        TargetUserId = targetUserId;
    }
}
