using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Administration.Notes;
using Content.Shared.Administration.Notes;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Chat.Managers;

public sealed class ChatSanitizationManager : IChatSanitizationManager
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IAdminNotesManager _adminNotes = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private const string HiFallback = "Hi.";
    private const string UnintelligibleAction = "attempted to say something unintelligible.";

    private static readonly string[] BlockedTerms =
    [
        "gay",
        "lesbian",
        "bisexual",
        "homosexual",
        "queer",
        "trans",
        "transgender",
        "nonbinary",
        "non-binary",
        "pansexual",
        "asexual",
        "intersex",
        "homo",
        "dyke",
        "fag",
        "faggot",
        "tranny",
        "nigger",
        "nigga",
        "kike",
        "spic",
        "chink",
        "gook",
        "wetback",
    ];

    private static readonly Regex[] BlockedTermRegexes = BuildBlockedTermRegexes();

    private static readonly Dictionary<string, string> SmileyToEmote = new()
    {
        // I could've done this with regex, but felt it wasn't the right idea.
        { ":)", "chatsan-smiles" },
        { ":]", "chatsan-smiles" },
        { "=)", "chatsan-smiles" },
        { "=]", "chatsan-smiles" },
        { "(:", "chatsan-smiles" },
        { "[:", "chatsan-smiles" },
        { "(=", "chatsan-smiles" },
        { "[=", "chatsan-smiles" },
        { "^^", "chatsan-smiles" },
        { "^-^", "chatsan-smiles" },
        { ":(", "chatsan-frowns" },
        { ":[", "chatsan-frowns" },
        { "=(", "chatsan-frowns" },
        { "=[", "chatsan-frowns" },
        { "):", "chatsan-frowns" },
        { ")=", "chatsan-frowns" },
        { "]:", "chatsan-frowns" },
        { "]=", "chatsan-frowns" },
        { ":D", "chatsan-smiles-widely" },
        { "D:", "chatsan-frowns-deeply" },
        { ":O", "chatsan-surprised" },
        { ":3", "chatsan-smiles" }, //nope
        { ":S", "chatsan-uncertain" },
        { ":>", "chatsan-grins" },
        { ":<", "chatsan-pouts" },
        { "xD", "chatsan-laughs" },
        { ":'(", "chatsan-cries" },
        { ":'[", "chatsan-cries" },
        { "='(", "chatsan-cries" },
        { "='[", "chatsan-cries" },
        { ")':", "chatsan-cries" },
        { "]':", "chatsan-cries" },
        { ")'=", "chatsan-cries" },
        { "]'=", "chatsan-cries" },
        { ";-;", "chatsan-cries" },
        { ";_;", "chatsan-cries" },
        { "qwq", "chatsan-cries" },
        { ":u", "chatsan-smiles-smugly" },
        { ":v", "chatsan-smiles-smugly" },
        { ">:i", "chatsan-annoyed" },
        { ":i", "chatsan-sighs" },
        { ":|", "chatsan-sighs" },
        { ":p", "chatsan-stick-out-tongue" },
        { ";p", "chatsan-stick-out-tongue" },
        { ":b", "chatsan-stick-out-tongue" },
        { "0-0", "chatsan-wide-eyed" },
        { "o-o", "chatsan-wide-eyed" },
        { "o.o", "chatsan-wide-eyed" },
        { "._.", "chatsan-surprised" },
        { ".-.", "chatsan-confused" },
        { "-_-", "chatsan-unimpressed" },
        { "smh", "chatsan-unimpressed" },
        { "smh.", "chatsan-unimpressed" },
        { "o/", "chatsan-waves" },
        { "^^/", "chatsan-waves" },
        { ":/", "chatsan-uncertain" },
        { ":\\", "chatsan-uncertain" },
        { "lmao", "chatsan-laughs" },
        { "lmao.", "chatsan-laughs" },
        { "lmfao", "chatsan-laughs" },
        { "lmfao.", "chatsan-laughs" },
        { "lol", "chatsan-laughs" },
        { "lol.", "chatsan-laughs" },
        { "lel", "chatsan-laughs" },
        { "lel.", "chatsan-laughs" },
        { "kek", "chatsan-laughs" },
        { "kek.", "chatsan-laughs" },
        { "rofl", "chatsan-laughs" },
        { "rofl.", "chatsan-laughs" },
        { "o7", "chatsan-salutes" },
        { ";_;7", "chatsan-tearfully-salutes"},
        { "idk", "chatsan-shrugs" },
        { "idk.", "chatsan-shrugs" },
        { "bruh", "chatsan-unimpressed" },
        { "bruh.", "chatsan-unimpressed" },
        { ";)", "chatsan-winks" },
        { ";]", "chatsan-winks" },
        { "(;", "chatsan-winks" },
        { "[;", "chatsan-winks" },
        { ":')", "chatsan-tearfully-smiles" },
        { ":']", "chatsan-tearfully-smiles" },
        { "=')", "chatsan-tearfully-smiles" },
        { "=']", "chatsan-tearfully-smiles" },
        { "(':", "chatsan-tearfully-smiles" },
        { "[':", "chatsan-tearfully-smiles" },
        { "('=", "chatsan-tearfully-smiles" },
        { "['=", "chatsan-tearfully-smiles" },
        // Corvax-Localization-Start
        { "0_o", "chatsan-wide-eyed" },
        { ":0", "chatsan-surprised" },
        { "T_T", "chatsan-cries" },
        { "=_(", "chatsan-cries" },
        { "!s", "chatsan-laughs" },
        { "!v", "chatsan-sighs" },
        { "!kh", "chatsan-claps" },
        { "!sh", "chatsan-snaps" },
        { "))", "chatsan-smiles-widely" },
        { ")", "chatsan-smiles" },
        { "((", "chatsan-frowns-deeply" },
        { "(", "chatsan-frowns" },
        { "masturbates", "prays" },
        { "screws", "prays" },
        { "has sex", "prays" },
        { "poops", "prays" },
        { "pees", "prays" },
        { "peed on", "prayed" },
        { "salutes", "hits self in the face" },
        { "saluted", "hits self in the face" },
        { "threw a heavy punch", "hits self in the face" },
        { "threw a sloppy punch", "hits self in the face" },
        // Corvax-Localization-End
    };

    private bool _doSanitize;

    // Anti-Goida
    private static readonly Regex GoydaRegex = new(@"[Gg][Oo]+[Yy][Dd][Aa]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void Initialize()
    {
        _configurationManager.OnValueChanged(CCVars.ChatSanitizerEnabled, x => _doSanitize = x, true);
    }

    public bool TryGetBlockedChatResult(string input, ChatSanitizationChannel channel, [NotNullWhen(true)] out BlockedChatMessageResult? result)
    {
        result = null;

        if (!_doSanitize || string.IsNullOrWhiteSpace(input))
            return false;

        foreach (var regex in BlockedTermRegexes)
        {
            if (!regex.IsMatch(input))
                continue;

            result = channel == ChatSanitizationChannel.OutOfCharacter
                ? new BlockedChatMessageResult(HiFallback, false)
                : new BlockedChatMessageResult(UnintelligibleAction, true);
            return true;
        }

        return false;
    }

    public void ReportBlockedChat(ICommonSession player, string rawMessage, string contextLabel)
    {
        var trimmed = rawMessage.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        _chatManager.SendAdminAlert($"Auto-moderation blocked {player.Name} ({player.UserId}) in {contextLabel}: \"{trimmed}\"");
        CreateModerationNote(player, trimmed, contextLabel);
    }

    public bool TrySanitizeOutSmilies(string input, EntityUid speaker, out string sanitized, [NotNullWhen(true)] out string? emote)
    {
        if (!_doSanitize)
        {
            sanitized = input;
            emote = null;
            return false;
        }

        input = input.TrimEnd();

        // Apply Anti-Goida filter
        input = GoydaRegex.Replace(input, "I am an idiot");

        foreach (var (smiley, replacement) in SmileyToEmote)
        {
            if (input.EndsWith(smiley, true, CultureInfo.InvariantCulture))
            {
                sanitized = input.Remove(input.Length - smiley.Length).TrimEnd();
                emote = Loc.GetString(replacement, ("ent", speaker));
                return true;
            }
        }

        sanitized = input;
        emote = null;
        return false;
    }

    private async void CreateModerationNote(ICommonSession player, string rawMessage, string contextLabel)
    {
        var noteMessage = $"Automatic chat moderation blocked a message in {contextLabel}. Raw attempted message: \"{rawMessage}\"";
        await _adminNotes.AddSystemRemark(player.UserId, NoteType.Note, noteMessage, NoteSeverity.Medium, true, null);
    }

    private static Regex[] BuildBlockedTermRegexes()
    {
        var regexes = new Regex[BlockedTerms.Length];

        for (var i = 0; i < BlockedTerms.Length; i++)
        {
            regexes[i] = BuildBlockedTermRegex(BlockedTerms[i]);
        }

        return regexes;
    }

    private static Regex BuildBlockedTermRegex(string term)
    {
        var pattern = new StringBuilder(@"(?<![\p{L}\p{N}])");
        var appendSeparator = false;

        foreach (var character in term)
        {
            if (character is ' ' or '-' or '_')
            {
                pattern.Append(@"[\W_]*");
                appendSeparator = false;
                continue;
            }

            if (appendSeparator)
                pattern.Append(@"[\W_]*");

            pattern.Append(GetProtectedCharacterPattern(character));
            appendSeparator = true;
        }

        pattern.Append(@"(?![\p{L}\p{N}])");
        return new Regex(pattern.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string GetProtectedCharacterPattern(char character)
    {
        return char.ToLowerInvariant(character) switch
        {
            'a' => "[a4@]",
            'b' => "[b8]",
            'e' => "[e3]",
            'g' => "[g69]",
            'i' => "[i1!|]",
            'l' => "[l1|]",
            'o' => "[o0]",
            's' => "[s5$]",
            't' => "[t7+]",
            'z' => "[z2]",
            _ => Regex.Escape(character.ToString()),
        };
    }
}
