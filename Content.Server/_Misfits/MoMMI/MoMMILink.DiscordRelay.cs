// #Misfits Change - Discord relay for in-game OOC and admin chat
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server.Discord;
using Content.Shared.CCVar;

namespace Content.Server.MoMMI;

internal sealed partial class MoMMILink
{
    [Dependency] private readonly DiscordWebhook _discordWebhook = default!;

    private const int OocEmbedColor = 0x3B82F6;
    private const int AdminEmbedColor = 0xF59E0B;
    private const int MaxDiscordUsernameLength = 80;
    private const int MaxDiscordDescriptionLength = 4000;

    private WebhookIdentifier? _oocWebhookIdentifier;
    private WebhookIdentifier? _adminChatWebhookIdentifier;
    private string _discordRelayServerName = string.Empty;
    private bool _discordRelaySubscribed;

    [GeneratedRegex(@"^https://(?:(?:canary|ptb)\.)?discord\.com/api/webhooks/(\d+)/((?!.*/).*)$")]
    private static partial Regex DiscordWebhookRegex();

    partial void PostInjectDiscordRelay()
    {
        // CVar subscriptions are deferred to first relay call.
        // Subscribing here (during PostInject / IoC BuildGraph) causes a KeyNotFoundException in
        // unit tests because CVars are loaded after BuildGraph runs in the test framework.
    }

    // Lazily subscribes to Discord relay CVars the first time a message is relayed.
    // By that point the config manager will have loaded all CVars.
    private void EnsureDiscordRelaySubscribed()
    {
        if (_discordRelaySubscribed)
            return;

        _discordRelaySubscribed = true;
        _configurationManager.OnValueChanged(CCVars.DiscordOOCWebhook, OnOOCWebhookChanged, true);
        _configurationManager.OnValueChanged(CCVars.DiscordAdminChatWebhook, OnAdminChatWebhookChanged, true);
        _configurationManager.OnValueChanged(Robust.Shared.CVars.GameHostName, value => _discordRelayServerName = value, true);
    }

    partial void RelayOOCToDiscord(string sender, string message)
    {
        EnsureDiscordRelaySubscribed();
        if (_oocWebhookIdentifier is not { } identifier)
            return;

        _ = RelayChatToDiscordAsync(identifier, "In-Game OOC", sender, message, OocEmbedColor);
    }

    partial void RelayAdminChatToDiscord(string sender, string message)
    {
        EnsureDiscordRelaySubscribed();
        if (_adminChatWebhookIdentifier is not { } identifier)
            return;

        _ = RelayChatToDiscordAsync(identifier, "In-Game Admin", sender, message, AdminEmbedColor);
    }

    private void OnOOCWebhookChanged(string url)
    {
        _oocWebhookIdentifier = ParseWebhookIdentifier(url, "OOC");
    }

    private void OnAdminChatWebhookChanged(string url)
    {
        _adminChatWebhookIdentifier = ParseWebhookIdentifier(url, "admin chat");
    }

    private WebhookIdentifier? ParseWebhookIdentifier(string url, string relayName)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var match = DiscordWebhookRegex().Match(url.Trim());
        if (!match.Success || match.Groups.Count <= 2)
        {
            _sawmill.Warning($"Could not parse Discord webhook URL for {relayName} relay.");
            return null;
        }

        return new WebhookIdentifier(match.Groups[1].Value, match.Groups[2].Value);
    }

    private async Task RelayChatToDiscordAsync(WebhookIdentifier identifier, string title, string sender, string message, int color)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            var payload = new WebhookPayload
            {
                Username = TrimForDiscord($"{sender} [{title}]", MaxDiscordUsernameLength),
                Embeds = new List<WebhookEmbed>
                {
                    new()
                    {
                        Description = TrimForDiscord(message, MaxDiscordDescriptionLength),
                        Color = color,
                        Footer = string.IsNullOrWhiteSpace(_discordRelayServerName)
                            ? null
                            : new WebhookEmbedFooter
                            {
                                Text = _discordRelayServerName,
                            },
                    },
                },
            };

            var response = await _discordWebhook.CreateMessage(identifier, payload);
            if (response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync();
            _sawmill.Warning($"Discord {title} relay returned status {(int) response.StatusCode}: {content}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while relaying {title} to Discord:\n{e}");
        }
    }

    private static string TrimForDiscord(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }
}