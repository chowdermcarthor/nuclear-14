// #Misfits Add - Server system for Vault-Tec Retractable Blades augment.
// Handles blade toggle action: spawns/despawns a melee weapon in the user's hand.

using Content.Shared._Misfits.Augments;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Misfits.Augments;

/// <summary>
/// Manages retractable blade deployment/retraction via toggle action.
/// Spawns a blade weapon entity in the performer's hand on deploy,
/// deletes it on retract or when the implant is removed.
/// </summary>
public sealed class AugmentRetractableBladesSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Toggle event fires on the implant entity (action container)
        SubscribeLocalEvent<AugmentRetractableBladesComponent, ToggleRetractableBladesEvent>(OnToggle);
        // Clean up spawned blade if the implant is destroyed or removed
        SubscribeLocalEvent<AugmentRetractableBladesComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnToggle(EntityUid uid, AugmentRetractableBladesComponent comp,
        ToggleRetractableBladesEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        if (comp.Deployed)
            Retract(uid, comp, performer);
        else
            Deploy(uid, comp, performer);

        args.Handled = true;
    }

    /// <summary>Extend the blades — spawn a weapon entity in the performer's hand.</summary>
    private void Deploy(EntityUid uid, AugmentRetractableBladesComponent comp, EntityUid performer)
    {
        var blade = Spawn(comp.BladePrototype, Transform(performer).Coordinates);

        if (_hands.TryPickupAnyHand(performer, blade))
        {
            comp.BladeEntity = blade;
            comp.Deployed = true;
            _audio.PlayPvs(comp.DeploySound, performer);
            _popup.PopupEntity(Loc.GetString("augment-retractable-blades-deploy"), performer, performer);
        }
        else
        {
            // No free hand available — can't deploy
            QueueDel(blade);
            _popup.PopupEntity(Loc.GetString("augment-retractable-blades-no-hand"), performer, performer);
        }
    }

    /// <summary>Retract the blades — delete the spawned weapon entity.</summary>
    private void Retract(EntityUid uid, AugmentRetractableBladesComponent comp, EntityUid performer)
    {
        if (comp.BladeEntity is { } blade && !Deleted(blade))
            QueueDel(blade);

        comp.BladeEntity = null;
        comp.Deployed = false;
        _audio.PlayPvs(comp.RetractSound, performer);
        _popup.PopupEntity(Loc.GetString("augment-retractable-blades-retract"), performer, performer);
    }

    private void OnShutdown(EntityUid uid, AugmentRetractableBladesComponent comp, ComponentShutdown args)
    {
        // If removed while deployed, clean up the blade entity
        if (comp.BladeEntity is { } blade && !Deleted(blade))
            QueueDel(blade);
    }
}
