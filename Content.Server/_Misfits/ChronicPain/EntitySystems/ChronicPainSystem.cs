// #Misfits Change - Ported from Delta-V chronic pain system
using Content.Server.Chat.Managers;
using Content.Shared._Misfits.ChronicPain.Components;
using Content.Shared._Misfits.ChronicPain.EntitySystems;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._Misfits.ChronicPain.EntitySystems;

/// <summary>
///     Server-side chronic pain handler. Sends pain messages as private emote-channel
///     messages so only the affected player sees them — no visible popup to others.
/// </summary>
public sealed class ChronicPainSystem : SharedChronicPainSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // #Misfits Change — send as a private self-emote (ChatChannel.Emotes to the owning session only)
    protected override void ShowPainEffect(Entity<ChronicPainComponent> entity, string message)
    {
        if (!_playerManager.TryGetSessionByEntity(entity.Owner, out var session))
            return;

        _chatManager.ChatMessageToOne(
            ChatChannel.Emotes,
            message,
            message,
            EntityUid.Invalid,
            false,
            session.Channel);
    }
}
