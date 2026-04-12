// #Misfits Add - Server system for Vault-Tec Targeting HUD augment.
// Manages the passive targeting HUD lifecycle and gun spread reduction.

using Content.Shared._Misfits.Augments;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._Misfits.Augments;

/// <summary>
/// Manages the targeting HUD implant lifecycle:
/// adds/removes the active marker on the body when the implant is inserted/removed,
/// and applies gun spread reduction via the GunRefreshModifiers event.
/// </summary>
public sealed class AugmentTargetingHudSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Implant lifecycle
        SubscribeLocalEvent<AugmentTargetingHudComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<AugmentTargetingHudComponent, ComponentShutdown>(OnShutdown);

        // Gun spread modifier (broadcast on gun entity)
        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnImplanted(EntityUid uid, AugmentTargetingHudComponent comp,
        ref ImplantImplantedEvent args)
    {
        if (args.Implanted is not { } body)
            return;

        // Place passive marker on the body
        var active = EnsureComp<AugmentTargetingHudActiveComponent>(body);
        active.SpreadReduction = comp.SpreadReduction;
        Dirty(body, active);

        _popup.PopupEntity(Loc.GetString("augment-targeting-hud-online"), body, body);
    }

    private void OnShutdown(EntityUid uid, AugmentTargetingHudComponent comp, ComponentShutdown args)
    {
        // Find the body this implant was in and remove the marker
        if (!TryComp<SubdermalImplantComponent>(uid, out var implant)
            || implant.ImplantedEntity is not { } body)
            return;

        if (TerminatingOrDeleted(body))
            return;

        RemCompDeferred<AugmentTargetingHudActiveComponent>(body);
    }

    private void OnGunRefreshModifiers(ref GunRefreshModifiersEvent args)
    {
        // Walk transform parent to find the entity holding this gun
        var holder = Transform(args.Gun.Owner).ParentUid;

        if (!TryComp<AugmentTargetingHudActiveComponent>(holder, out var comp))
            return;

        // Reduce spread — lower angles = tighter grouping
        var keepFraction = 1.0 - comp.SpreadReduction;
        args.MinAngle = new Angle((double) args.MinAngle * keepFraction);
        args.MaxAngle = new Angle((double) args.MaxAngle * keepFraction);
        args.AngleIncrease = new Angle((double) args.AngleIncrease * keepFraction);
    }
}
