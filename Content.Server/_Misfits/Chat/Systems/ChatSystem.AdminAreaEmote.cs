// #Misfits Change
// ChatSystem partial — admin area (local-radius) emote in green text, no speaker name, no bubble.
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    /// <summary>
    ///     Sends a green-coloured nameless ambient emote to all players in normal voice range
    ///     of <paramref name="source"/> (like /do but green).  Admin-only; no action-blocker
    ///     or rate-limit checks are applied.
    ///     Uses <see cref="EntityUid.Invalid"/> as the message source so no speech bubble
    ///     appears over the admin's head.
    /// </summary>
    public void TrySendAdminAreaEmote(
        EntityUid source,
        string action,
        ICommonSession player)
    {
        if (string.IsNullOrWhiteSpace(action))
            return;

        if (_sanitizer.TryGetBlockedChatResult(action, ChatSanitizationChannel.InCharacter, out var moderation))
        {
            _sanitizer.ReportBlockedChat(player, action, "admin area emote");
            SendEntityEmote(source, moderation.ReplacementText, ChatTransmitRange.Normal, null, _language.GetLanguage(source), ignoreActionBlocker: true, author: player.UserId);
            return;
        }

        // Record the player's entity for admin-log history.
        _chatManager.EnsurePlayer(player.UserId).AddEntity(GetNetEntity(source));

        var formatted = FormatRoleplayActionMarkup(action);

        var wrappedMessage = Loc.GetString(
            "chat-admin-area-emote-wrap-message",
            ("message", formatted));

        // Send to everyone in voice range but with EntityUid.Invalid as the message entity
        // so no speech bubble appears over the admin's head.
        foreach (var (session, data) in GetRecipients(source, Transform(source).GridUid == null ? 0.3f : VoiceRange))
        {
            if (session.AttachedEntity != null
                && Transform(session.AttachedEntity.Value).GridUid != Transform(source).GridUid
                && !CheckAttachedGrids(source, session.AttachedEntity.Value))
                continue;

            var entRange = MessageRangeCheck(session, data, ChatTransmitRange.Normal);
            if (entRange == MessageRangeCheckResult.Disallowed)
                continue;

            var entHideChat = entRange == MessageRangeCheckResult.HideChat;

            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                action,
                wrappedMessage,
                EntityUid.Invalid,        // no entity → no bubble
                entHideChat,
                session.Channel,
                author: player.UserId);
        }

        _replay.RecordServerMessage(
            new ChatMessage(ChatChannel.Emotes, action, wrappedMessage, default, null, false));

        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"Admin area-emote from {player:Player}: {action}");
    }
}
