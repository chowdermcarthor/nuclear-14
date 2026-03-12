// #Misfits Add: Broadcasts an emote chat message to nearby players when a player points at an entity.
// Ghosts are routed to Dead chat instead of the IC emote channel.
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Pointing;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Pointing;

/// <summary>
/// Hooks <see cref="AfterPointedAtEvent"/> to send a local-area emote chat message so nearby
/// players see "* John Smith points at Jane Doe *" in the emote channel whenever someone points.
/// Ghosts are routed to Dead chat so other ghosts can see the gesture.
/// </summary>
public sealed class PointingChatSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to the event raised on the pointer entity after a successful point.
        SubscribeLocalEvent<MetaDataComponent, AfterPointedAtEvent>(OnAfterPointed);
    }

    /// <summary>
    /// Sends a chat message when a player points at another entity.
    /// Ghosts are routed to Dead chat; living players get a local-area emote.
    /// Bypasses the action blocker because pointing is already gated by PointingSystem.
    /// </summary>
    private void OnAfterPointed(EntityUid uid, MetaDataComponent component, ref AfterPointedAtEvent ev)
    {
        // Use a dedicated reflexive string for self-pointing so the chat line reads
        // "<name> points at themselves" instead of formatting the actor as a target name.
        var message = uid == ev.Pointed
            ? Loc.GetString("pointing-chat-point-at-self")
            : Loc.GetString("pointing-chat-point-at-other", ("other", Identity.Entity(ev.Pointed, EntityManager)));

        if (HasComp<GhostComponent>(uid))
        {
            // TrySendInGameICMessage silently drops ghost messages when no player session is
            // passed (the OOC guard requires a non-null session). Resolve the session and call
            // TrySendInGameOOCMessage directly so the Dead-chat path completes.
            if (!_playerManager.TryGetSessionByEntity(uid, out var session))
                return;

            _chat.TrySendInGameOOCMessage(uid, message, InGameOOCChatType.Dead, false, player: session);
            return;
        }

        // Living players: send as a local-area emote — visible to nearby players in the Emotes channel.
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Emote,
            ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }
}
