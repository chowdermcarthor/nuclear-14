using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;

namespace Content.Server.Chat.Managers;

public enum ChatSanitizationChannel
{
    InCharacter,
    OutOfCharacter,
}

public sealed record BlockedChatMessageResult(string ReplacementText, bool UseEmoteReplacement);

public interface IChatSanitizationManager
{
    public void Initialize();

    public bool TrySanitizeOutSmilies(string input, EntityUid speaker, out string sanitized, [NotNullWhen(true)] out string? emote);

    public bool TryGetBlockedChatResult(string input, ChatSanitizationChannel channel, [NotNullWhen(true)] out BlockedChatMessageResult? result);

    public void ReportBlockedChat(ICommonSession player, string rawMessage, string contextLabel);
}
