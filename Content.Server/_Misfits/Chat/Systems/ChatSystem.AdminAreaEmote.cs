// #Misfits Change
// ChatSystem partial — admin area (local-radius) emote in green text, no speaker name.
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
    /// </summary>
    public void TrySendAdminAreaEmote(
        EntityUid source,
        string action,
        ICommonSession player)
    {
        if (string.IsNullOrWhiteSpace(action))
            return;

        // Record the player's entity for admin-log history.
        _chatManager.EnsurePlayer(player.UserId).AddEntity(GetNetEntity(source));

        var formatted = FormatRoleplayActionMarkup(action);

        var wrappedMessage = Loc.GetString(
            "chat-admin-area-emote-wrap-message",
            ("message", formatted));

        SendInVoiceRange(
            ChatChannel.Emotes,
            string.Empty,
            action,
            wrappedMessage,
            obfuscated:               string.Empty,
            obfuscatedWrappedMessage: string.Empty,
            source,
            ChatTransmitRange.Normal,
            player.UserId);

        _adminLogger.Add(
            LogType.Chat,
            LogImpact.Low,
            $"Admin area-emote from {player:Player}: {action}");
    }
}
