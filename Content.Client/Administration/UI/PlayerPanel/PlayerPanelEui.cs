using Content.Client.Administration.Managers;
using Content.Client._Misfits.Administration.UI; // #Misfits Add — for QuickBwoinkWindow
using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Client.Administration.UI.PlayerPanel;

[UsedImplicitly]
public sealed class PlayerPanelEui : BaseEui
{
    [Dependency] private readonly IClientConsoleHost _console = default!;
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IClipboardManager _clipboard = default!;

    private PlayerPanel PlayerPanel { get;  }

    // #Misfits Add — focused quick-reply window instance (one at a time)
    private QuickBwoinkWindow? _quickBwoinkWindow;

    public PlayerPanelEui()
    {
        PlayerPanel = new PlayerPanel(_admin);

        PlayerPanel.OnUsernameCopy += username => _clipboard.SetText(username);
        PlayerPanel.OnOpenNotes += id => _console.ExecuteCommand($"adminnotes \"{id}\"");
        // Kick command does not support GUIDs
        PlayerPanel.OnKick += username => _console.ExecuteCommand($"kick \"{username}\"");
        PlayerPanel.OnOpenBanPanel += id => _console.ExecuteCommand($"banpanel \"{id}\"");
        PlayerPanel.OnOpenBans += id => _console.ExecuteCommand($"banlist \"{id}\"");
        // #Misfits Change — open a focused quick-reply window instead of the full admin panel
        // This prevents accidental messaging of the wrong player.
        PlayerPanel.OnAhelp += id =>
        {
            if (id == null)
                return;

            // Reuse existing window if it targets the same player
            if (_quickBwoinkWindow is { Disposed: false })
            {
                _quickBwoinkWindow.MoveToFront();
                return;
            }

            var targetName = PlayerPanel.TargetUsername ?? id.Value.ToString();
            _quickBwoinkWindow = new QuickBwoinkWindow(id.Value, targetName);
            _quickBwoinkWindow.OnClose += () => _quickBwoinkWindow = null;
            _quickBwoinkWindow.OpenCentered();
        };
        PlayerPanel.OnWhitelistToggle += (id, whitelisted) =>
        {
            _console.ExecuteCommand(whitelisted ? $"whitelistremove \"{id}\"" : $"whitelistadd \"{id}\"");
        };

        PlayerPanel.OnFreezeAndMuteToggle += () => SendMessage(new PlayerPanelFreezeMessage(true));
        PlayerPanel.OnFreeze += () => SendMessage(new PlayerPanelFreezeMessage());
        PlayerPanel.OnLogs += () => SendMessage(new PlayerPanelLogsMessage());
        PlayerPanel.OnRejuvenate += () => SendMessage(new PlayerPanelRejuvenationMessage());
        PlayerPanel.OnDelete += () => SendMessage(new PlayerPanelDeleteMessage());
        // #Misfits Change — follow the target player's current entity from the player panel.
        PlayerPanel.OnFollow += entity =>
        {
            if (entity is { } netEntity && netEntity != NetEntity.Invalid)
                _console.ExecuteCommand($"follow \"{netEntity}\"");
        };

        // #Misfits Change — ghost follow: enter aghost mode then orbit the target player.
        PlayerPanel.OnGhostFollow += entity =>
        {
            if (entity is { } netEntity && netEntity != NetEntity.Invalid)
                SendMessage(new PlayerPanelGhostFollowMessage());
        };

        // #Misfits Add — respawn/despawn: deletes the character and frees the spawn slot.
        PlayerPanel.OnRespawn += () => SendMessage(new PlayerPanelRespawnMessage());

        PlayerPanel.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        PlayerPanel.OpenCentered();
    }

    public override void Closed()
    {
        PlayerPanel.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not PlayerPanelEuiState s)
            return;

        PlayerPanel.TargetPlayer = s.Guid;
        PlayerPanel.TargetUsername = s.Username;
        PlayerPanel.TargetNetEntity = s.AttachedEntity;
        PlayerPanel.SetUsername(s.Username);
        PlayerPanel.SetPlaytime(s.Playtime);
        PlayerPanel.SetBans(s.TotalBans, s.TotalRoleBans);
        PlayerPanel.SetNotes(s.TotalNotes);
        PlayerPanel.SetWhitelisted(s.Whitelisted);
        PlayerPanel.SetSharedConnections(s.SharedConnections);
        PlayerPanel.SetFrozen(s.CanFreeze, s.Frozen);
        PlayerPanel.SetAhelp(s.CanAhelp);
        PlayerPanel.SetButtons();
    }
}
