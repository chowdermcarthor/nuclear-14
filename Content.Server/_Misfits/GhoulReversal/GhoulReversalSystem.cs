// #Misfits Change
using System.Threading.Tasks;
using Content.Server._Misfits.GhoulReversal;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Server.Ghoul;

namespace Content.Server._Misfits.GhoulReversal;

/// <summary>
/// Handles reversing ghoul species back to human when injected with a de-ghoulification syringe.
/// Updates both the in-game entity and database character profile so the change persists.
/// </summary>
public sealed class GhoulReversalSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private int MaxCharacterSlots => _cfg.GetCVar(CCVars.GameMaxCharacterSlots);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhoulReversalComponent, InjectorDoAfterEvent>(OnInjectorDoAfter);
    }

    private void OnInjectorDoAfter(EntityUid uid, GhoulReversalComponent component, InjectorDoAfterEvent args)
    {
        if (args.Cancelled || args.Args.Target == null)
            return;

        var target = args.Args.Target.Value;
        var user = args.Args.User;

        if (!HasComp<HumanoidAppearanceComponent>(target) || !HasComp<MobStateComponent>(target))
            return;

        ReverseGhoul(target, user, component);
    }

    private async void ReverseGhoul(EntityUid target, EntityUid user, GhoulReversalComponent component)
    {
        if (!Exists(target) || Deleted(target))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(target, out var appearance))
            return;

        // Only work on ghoul species
        if (!component.GhoulSpecies.Contains(appearance.Species))
        {
            _popup.PopupEntity(Loc.GetString(component.NotGhoulMessage), target, user);
            return;
        }

        // Validate target species prototype exists
        if (!_prototype.TryIndex<SpeciesPrototype>(component.TargetSpecies, out _))
        {
            Log.Error($"GhoulReversalSystem: TargetSpecies '{component.TargetSpecies}' is not a valid species prototype.");
            return;
        }

        _popup.PopupEntity(Loc.GetString(component.TransformationMessage), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(
            Loc.GetString(component.TransformationOthersMessage, ("target", target)),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);

        // Revert species in-game
        _humanoid.SetSpecies(target, component.TargetSpecies);

        // Remove the feral tracker so they don't go feral after reversal
        RemCompDeferred<FeralGhoulifyComponent>(target);

        // Update the database profile so the reversion persists across rounds
        if (component.UpdateDatabaseProfile && _mind.TryGetMind(target, out var mindId, out var mind))
        {
            if (mind.Session != null)
            {
                var prefs = _prefs.GetPreferences(mind.Session.UserId);
                await UpdateCharacterProfile(mind.Session, prefs.SelectedCharacterIndex, component);
            }
        }
    }

    private async Task UpdateCharacterProfile(ICommonSession session, int slot, GhoulReversalComponent component)
    {
        try
        {
            var userId = session.UserId;
            var prefs = _prefs.GetPreferences(userId);
            if (prefs == null || !prefs.Characters.TryGetValue(slot, out var profile))
                return;

            if (profile is not HumanoidCharacterProfile humanoidProfile)
                return;

            // Revert the species to human in the saved profile
            var newProfile = humanoidProfile.WithSpecies(component.TargetSpecies);
            await _prefs.SetProfile(userId, slot, newProfile);

            SendPreferencesToClient(session);
        }
        catch (Exception ex)
        {
            Log.Error($"GhoulReversalSystem: Failed to update character profile during ghoul reversal: {ex}");
        }
    }

    private void SendPreferencesToClient(ICommonSession session)
    {
        try
        {
            var prefs = _prefs.GetPreferences(session.UserId);
            var msg = new MsgPreferencesAndSettings
            {
                Preferences = prefs,
                Settings = new GameSettings
                {
                    MaxCharacterSlots = MaxCharacterSlots
                }
            };
            _netManager.ServerSendMessage(msg, session.Channel);
        }
        catch (Exception ex)
        {
            Log.Error($"GhoulReversalSystem: Failed to send updated preferences to client: {ex}");
        }
    }
}
