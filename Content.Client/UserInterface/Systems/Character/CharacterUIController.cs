using System.Linq;
using Content.Client._Misfits.Currency;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Character.Controls;
using Content.Client.UserInterface.Systems.Character.Windows;
using Content.Client.UserInterface.Systems.Objectives.Controls;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using static Content.Client.CharacterInfo.CharacterInfoSystem;
using static Robust.Client.UserInterface.Controls.BaseButton;
using Content.Shared.CharacterInfo;

namespace Content.Client.UserInterface.Systems.Character;

[UsedImplicitly]
public sealed class CharacterUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>, IOnSystemChanged<CharacterInfoSystem>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;
    [UISystemDependency] private readonly SpriteSystem _sprite = default!;
    // #Misfits Add - use existing CurrencyWalletSystem to open the full wallet window
    [UISystemDependency] private readonly CurrencyWalletSystem _currencyWallet = default!;

    private CharacterWindow? _window;
    // #Misfits Add - dedicated SPECIAL allocator sub-window
    private SpecialWindow? _specialWindow;

    // #Misfits Add - SPECIAL allocation UI state
    private static readonly string[] SpecialNames = { "Strength", "Perception", "Endurance", "Charisma", "Intelligence", "Agility", "Luck" };
    private static readonly string[] SpecialDesc =
    {
        "Increases melee damage and heavy weapon handling. Reduces movement penalties from heavy equipment.",
        "Improves ranged accuracy and reduces weapon recoil.",
        "Increases maximum health, radiation resistance, and stamina.",
        "Reduces prices from vendors. Improves social fluency and negotiations.",
        "Improves crafting quality. Higher values yield better results when crafting items.",
        "Increases movement speed.",
        "Improves loot quality when scavenging junkpiles and containers.",
    };
    private readonly int[] _allocValues      = { 1, 1, 1, 1, 1, 1, 1 };
    // #Misfits Add - saved copy of server values so Cancel can restore them
    private readonly int[] _savedAllocValues  = { 1, 1, 1, 1, 1, 1, 1 };
    private const int AllocBudget = 10;
    private int _allocPoints = AllocBudget;
    private bool _localStatsConfirmed;
    private bool _historyExpanded;
    private string _lastEntityName = string.Empty;
    private Direction _spriteDirection = Direction.South; // #Misfits Add - rotation state

    private MenuButton? CharacterButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.CharacterButton;

    public void OnStateEntered(GameplayState state)
    {
        DebugTools.Assert(_window == null);

        _window = UIManager.CreateWindow<CharacterWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterTop);

        // #Misfits Add - wire Character Menu buttons at creation time to avoid missed hook if LoadButton fires early
        _window.OpenSpecialButton.OnPressed    += _ => ToggleSpecialWindow();
        _window.OpenWalletButton.OnPressed     += _ => _currencyWallet.OpenWallet();
        // #Misfits - History disabled; button removed from XAML
        //_window.HistoryToggleButton.OnPressed  += _ => ToggleHistoryPanel();
        _window.SpriteRotateLeft.OnPressed     += _ => { _spriteDirection = _spriteDirection.TurnCw(); SetSpriteDirection(); };
        _window.SpriteRotateRight.OnPressed    += _ => { _spriteDirection = _spriteDirection.TurnCcw(); SetSpriteDirection(); };
        // Disable sub-window buttons until character data arrives
        _window.OpenSpecialButton.Disabled = true;
        _window.OpenWalletButton.Disabled = true;
        // #Misfits - History disabled
        //UpdateHistoryToggleLabel();
        // Create dedicated SPECIAL sub-window and wire all allocation buttons
        _specialWindow = UIManager.CreateWindow<SpecialWindow>();
        _specialWindow.SpecialApplyButton.OnPressed      += _ => OnSpecialApplyPressed();
        _specialWindow.SpecialCancelButton.OnPressed     += _ => ResetSpecialAllocation();
        _specialWindow.SpecialConfirmYesButton.OnPressed += _ => OnSpecialConfirmYes();
        _specialWindow.SpecialConfirmNoButton.OnPressed  += _ => OnSpecialConfirmNo();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenCharacterMenu,
                 InputCmdHandler.FromDelegate(_ => ToggleWindow()))
             .Register<CharacterUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        if (_window != null)
        {
            _window.Dispose();
            _window = null;
        }
        // #Misfits Add - dispose dedicated sub-windows
        _specialWindow?.Dispose();
        _specialWindow = null;

        CommandBinds.Unregister<CharacterUIController>();
    }

    public void OnSystemLoaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate += CharacterUpdated;
        _player.LocalPlayerDetached += CharacterDetached;
    }

    public void OnSystemUnloaded(CharacterInfoSystem system)
    {
        system.OnCharacterUpdate -= CharacterUpdated;
        _player.LocalPlayerDetached -= CharacterDetached;
    }

    public void UnloadButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.OnPressed -= CharacterButtonPressed;
    }

    public void LoadButton()
    {
        if (CharacterButton == null)
        {
            return;
        }

        CharacterButton.OnPressed += CharacterButtonPressed;

        if (_window == null)
        {
            return;
        }

        _window.OnClose += DeactivateButton;
        _window.OnOpen += ActivateButton;
    }

    private void DeactivateButton() => CharacterButton!.Pressed = false;
    private void ActivateButton() => CharacterButton!.Pressed = true;

    private void CharacterUpdated(CharacterData data)
    {
        if (_window == null)
        {
            return;
        }

        var (entity, job, objectives, briefing, entityName, specials, persistentStats) = data;

        _window.SpriteView.SetEntity(entity);
        _window.NameLabel.Text = entityName;
        _window.SubText.Text = job;
        _window.Objectives.RemoveAllChildren();
        _window.ObjectivesLabel.Visible = objectives.Any();
        _window.Specials.RemoveAllChildren();
        _window.SpecialsLabel.Visible = specials.Any();

        foreach (var (groupId, conditions) in objectives)
        {
            var objectiveControl = new CharacterObjectiveControl
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Modulate = Color.Gray
            };

            objectiveControl.AddChild(new Label
            {
                // #Misfits Tweak - map raw issuer prototype ID to proper faction display name
                Text = GetFactionDisplayName(groupId),
                Modulate = Color.LightSkyBlue
            });

            foreach (var condition in conditions)
            {
                var conditionControl = new ObjectiveConditionsControl();
                conditionControl.ProgressTexture.Texture = _sprite.Frame0(condition.Icon);
                conditionControl.ProgressTexture.Progress = condition.Progress;
                var titleMessage = new FormattedMessage();
                var descriptionMessage = new FormattedMessage();
                titleMessage.AddText(condition.Title);
                descriptionMessage.AddText(condition.Description);

                conditionControl.Title.SetMessage(titleMessage);
                conditionControl.Description.SetMessage(descriptionMessage);

                objectiveControl.AddChild(conditionControl);
            }

            _window.Objectives.AddChild(objectiveControl);
        }

        if (briefing != null)
        {
            var briefingControl = new ObjectiveBriefingControl();
            var text = new FormattedMessage();
            text.PushColor(Color.Yellow);
            text.AddText(briefing);
            briefingControl.Label.SetMessage(text);
            _window.Objectives.AddChild(briefingControl);
        }

        //Nuclear14 Special
        foreach (var special in specials)
        {
            var specialControl = new BoxContainer()
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Modulate = Color.Gray
            };

            specialControl.AddChild(new Label
            {
                Text = special,
            });
            _window.Specials.AddChild(specialControl);
        }

        _window.SpecialsPlaceholder.Visible = specials == null;
        //Nuclear14 end

        // #Misfits Add - populate dedicated SPECIAL window, history panel, and stats
        if (_specialWindow != null) _specialWindow.SpecialRows.RemoveAllChildren();
        _window.History.RemoveAllChildren();
        _window.Stats.RemoveAllChildren();

        if (persistentStats != null)
        {
            _localStatsConfirmed = persistentStats.StatsConfirmed;
            _lastEntityName = entityName;
            // #Misfits - History disabled
            //UpdateHistoryToggleLabel();

            // Populate dedicated SPECIAL allocation window
            if (_specialWindow != null)
            {
                RebuildSpecialRows(persistentStats);
                // If already locked show the locked label; otherwise show the normal Apply/Reset row
                _specialWindow.SpecialPointsLabel.Visible = !_localStatsConfirmed;
                _specialWindow.SpecialButtonRow.Visible   = !_localStatsConfirmed;
                _specialWindow.SpecialConfirmRow.Visible  = false;
                _specialWindow.SpecialLockedLabel.Visible = _localStatsConfirmed;
            }

            // ── Lifetime statistics ─────────────────────────────────────────────────
            _window.StatsLabel.Visible = true;
            _window.StatsPlaceholder.Visible = false;
            _window.Stats.AddChild(new Label
            {
                Text = $"Mob Kills: {persistentStats.MobKills}   Deaths: {persistentStats.Deaths}   Rounds: {persistentStats.RoundsPlayed}",
                Modulate = Color.LightGray,
            });

            // ── Character history log (disabled) ─────────────────────────────────────
            // _window.HistoryPlaceholder.Visible = persistentStats.HistoryLog.Count == 0;
            // foreach (var entry in persistentStats.HistoryLog)
            //     _window.History.AddChild(new Label { Text = $"• {entry}", Modulate = Color.White });

            // Ensure Character Menu buttons are enabled when data is available
            _window.OpenSpecialButton.Disabled = false;
            _window.OpenWalletButton.Disabled = false;
            // #Misfits - History button removed from XAML
            //_window.HistoryToggleButton.Visible = true;
        }
        else
        {
            // Disable/hide Misfits sections when no persistent data available (e.g., observer)
            _window.OpenSpecialButton.Disabled = true;
            _window.OpenWalletButton.Disabled = true;
            // #Misfits - History disabled
            //_window.HistoryToggleButton.Visible = false;
            _window.HistoryPanel.Visible = false;
            _window.StatsLabel.Visible = false;
            _window.StatsPlaceholder.Visible = false;
        }
        // #Misfits Add END

        var controls = _characterInfo.GetCharacterInfoControls(entity);
        foreach (var control in controls)
        {
            _window.Objectives.AddChild(control);
        }
    }

    private void CharacterDetached(EntityUid uid)
    {
        CloseWindow();
    }

    private void CharacterButtonPressed(ButtonEventArgs args)
    {
        ToggleWindow();
    }

    private void CloseWindow()
    {
        _window?.Close();
    }

    private void ToggleWindow()
    {
        if (_window == null)
            return;

        if (CharacterButton != null)
        {
            CharacterButton.SetClickPressed(!_window.IsOpen);
        }

        if (_window.IsOpen)
        {
            CloseWindow();
        }
        else
        {
            _characterInfo.RequestCharacterInfo();
            _window.Open();
        }
    }

    // #Misfits Add - window and panel toggle helpers
    private void ToggleSpecialWindow()
    {
        // Opens or closes the dedicated SPECIAL allocation window
        if (_specialWindow == null) return;
        if (_specialWindow.IsOpen)
            _specialWindow.Close();
        else
            _specialWindow.Open();
    }

    private void ToggleWalletWindow()
    {
        // Superseded: Currency Wallet button now calls _currencyWallet.OpenWallet() directly.
        // Kept to avoid breaking references; no longer invoked. #Misfits Change
    }

    private void ToggleHistoryPanel()
    {
        // #Misfits - History disabled
    }

    private void UpdateHistoryToggleLabel()
    {
        // #Misfits - History disabled
    }

    // #Misfits Add - one colour per S.P.E.C.I.A.L. stat, matching classic Fallout hues
    private static readonly Color[] SpecialColors =
    {
        new Color(0.88f, 0.32f, 0.32f), // Strength   – red
        new Color(0.88f, 0.72f, 0.32f), // Perception – amber
        new Color(0.32f, 0.77f, 0.32f), // Endurance  – green
        new Color(0.88f, 0.32f, 0.77f), // Charisma   – pink
        new Color(0.32f, 0.66f, 0.88f), // Intelligence – blue
        new Color(0.32f, 0.88f, 0.66f), // Agility    – teal
        new Color(0.88f, 0.88f, 0.32f), // Luck       – yellow
    };

    private void RebuildSpecialRows(CharacterPersistentStats stats)
    {
        if (_specialWindow == null) return;
        _specialWindow.SpecialRows.RemoveAllChildren();

        // Seed allocation values from server state
        _allocValues[0] = stats.Strength;
        _allocValues[1] = stats.Perception;
        _allocValues[2] = stats.Endurance;
        _allocValues[3] = stats.Charisma;
        _allocValues[4] = stats.Intelligence;
        _allocValues[5] = stats.Agility;
        _allocValues[6] = stats.Luck;
        // Save originals so Cancel can restore them
        Array.Copy(_allocValues, _savedAllocValues, SpecialNames.Length);
        _allocPoints = AllocBudget - (_allocValues.Sum() - SpecialNames.Length);
        UpdatePointsLabel();

        for (var idx = 0; idx < SpecialNames.Length; idx++)
        {
            var i = idx; // capture for closures
            _specialWindow.SpecialRows.AddChild(BuildSpecialRow(i));
        }
    }

    /// <summary>
    /// Builds one SPECIAL stat row styled to match the Job Slots panel layout:
    /// coloured name header, value label, and – / + buttons with OpenRight/OpenLeft styles.
    /// #Misfits Add
    /// </summary>
    private Control BuildSpecialRow(int i)
    {
        // Outer panel with dark background, same as job-slots department panel
        var panel = new PanelContainer { Margin = new Thickness(0, 0, 0, 2) };
        panel.StyleClasses.Add("BackgroundDark");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            Margin = new Thickness(6, 4, 6, 4),
        };

        // ── Top row: coloured name │ value │ − │ + ──────────────────────────
        var topRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        var nameLabel = new Label
        {
            Text = SpecialNames[i],
            Modulate = SpecialColors[i],
            MinWidth = 140,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        nameLabel.StyleClasses.Add("LabelHeading");

        // Value label – shows the current allocated value as a plain number
        var valueLbl = new Label
        {
            Text = _allocValues[i].ToString(),
            MinWidth = 80,
            VerticalAlignment = Control.VAlignment.Center,
        };

        var canEdit = !_localStatsConfirmed;

        var minusBtn = new Button { Text = "-", MinWidth = 30, Disabled = !canEdit };
        minusBtn.StyleClasses.Add("OpenRight");

        var plusBtn = new Button { Text = "+", MinWidth = 30, Disabled = !canEdit };
        plusBtn.StyleClasses.Add("OpenLeft");

        if (canEdit)
        {
            minusBtn.OnPressed += _ =>
            {
                if (_allocValues[i] <= 1) return;
                _allocValues[i]--;
                _allocPoints++;
                valueLbl.Text = _allocValues[i].ToString();
                UpdatePointsLabel();
            };
            plusBtn.OnPressed += _ =>
            {
                if (_allocValues[i] >= 10 || _allocPoints <= 0) return;
                _allocValues[i]++;
                _allocPoints--;
                valueLbl.Text = _allocValues[i].ToString();
                UpdatePointsLabel();
            };
        }

        topRow.AddChild(nameLabel);
        topRow.AddChild(valueLbl);
        topRow.AddChild(minusBtn);
        topRow.AddChild(plusBtn);

        // ── Description sub-label ─────────────────────────────────────────────
        var descLabel = new Label
        {
            Text = SpecialDesc[i],
            Modulate = Color.FromHex("#AAAAAA"),
            HorizontalExpand = true,
        };
        descLabel.StyleClasses.Add("LabelSubText");

        root.AddChild(topRow);
        root.AddChild(descLabel);
        panel.AddChild(root);
        return panel;
    }

    private void UpdatePointsLabel()
    {
        if (_specialWindow == null) return;
        _specialWindow.SpecialPointsLabel.Text = $"Available points: {_allocPoints}";
    }

    // #Misfits Add - Apply/Cancel/Confirm flow for SPECIAL allocation

    private void OnSpecialApplyPressed()
    {
        // Switch the bottom area to the confirmation prompt
        if (_specialWindow == null) return;
        _specialWindow.SpecialButtonRow.Visible  = false;
        _specialWindow.SpecialConfirmRow.Visible = true;
    }

    private void OnSpecialConfirmNo()
    {
        // Player changed their mind — go back to the normal Apply/Reset buttons
        if (_specialWindow == null) return;
        _specialWindow.SpecialConfirmRow.Visible = false;
        _specialWindow.SpecialButtonRow.Visible  = true;
    }

    private void OnSpecialConfirmYes()
    {
        // Permanently lock the allocation; send to server
        if (_specialWindow == null || _localStatsConfirmed) return;
        _characterInfo.SendConfirmSpecialAllocation(
            _allocValues[0], _allocValues[1], _allocValues[2], _allocValues[3],
            _allocValues[4], _allocValues[5], _allocValues[6]);

        // Immediately update local state so the UI reflects the lock without waiting for a server echo
        _localStatsConfirmed = true;
        _specialWindow.SpecialConfirmRow.Visible  = false;
        _specialWindow.SpecialButtonRow.Visible   = false;
        _specialWindow.SpecialPointsLabel.Visible = false;
        _specialWindow.SpecialLockedLabel.Visible = true;
        // Rebuild rows in disabled/read-only state
        _specialWindow.SpecialRows.RemoveAllChildren();
        for (var i = 0; i < SpecialNames.Length; i++)
            _specialWindow.SpecialRows.AddChild(BuildSpecialRow(i));
    }

    private void ResetSpecialAllocation()
    {
        // Restore working values to the server-sent originals and rebuild rows
        if (_specialWindow == null) return;
        Array.Copy(_savedAllocValues, _allocValues, SpecialNames.Length);
        _allocPoints = AllocBudget - (_allocValues.Sum() - SpecialNames.Length);
        _specialWindow.SpecialRows.RemoveAllChildren();
        for (var i = 0; i < SpecialNames.Length; i++)
            _specialWindow.SpecialRows.AddChild(BuildSpecialRow(i));
        UpdatePointsLabel();
        // Ensure we're back on the normal button row (in case the confirm prompt was up)
        _specialWindow.SpecialConfirmRow.Visible = false;
        _specialWindow.SpecialButtonRow.Visible  = true;
    }

    /// <summary>
    /// Maps raw objective issuer prototype IDs to human-readable faction display names.
    /// #Misfits Add - proper faction names in the Objectives section of Character Menu.
    /// </summary>
    private static string GetFactionDisplayName(string issuer) => issuer switch
    {
        "brotherhoodofsteel" => "Brotherhood of Steel",
        "caesarlegion"       => "Caesar's Legion",
        "ncr"                => "New California Republic",
        "dragon"             => "Dragon",
        "geometerofblood"    => "Geometer of Blood",
        "spiderclan"         => "Spider Clan",
        "syndicate"          => "Syndicate",
        "thief"              => "Thief",
        "self"               => "Personal",
        _                    => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(issuer),
    };

    // #Misfits Add - sprite direction control
    private void SetSpriteDirection()
    {
        if (_window == null) return;
        _window.SpriteView.OverrideDirection = (Direction)((int)_spriteDirection % 4 * 2);
    }
    // #Misfits Add END
}
