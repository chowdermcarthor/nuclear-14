// #Misfits Add - Server system for Vault-Tec Reflex Booster augment.
// Handles activation logic, timer expiry, and knockdown drawback.

using Content.Shared._Misfits.Augments;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Augments;

/// <summary>
/// Server-side activation and timer management for the reflex booster augment.
/// The shared system handles the actual speed modifier for prediction.
/// </summary>
public sealed class AugmentReflexBoosterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Action event fires on the implant entity (action container)
        SubscribeLocalEvent<AugmentReflexBoosterComponent, ActivateReflexBoosterEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, AugmentReflexBoosterComponent comp,
        ActivateReflexBoosterEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        // Can't stack — only one activation at a time
        if (HasComp<AugmentReflexBoosterActiveComponent>(performer))
            return;

        // Place the speed buff marker on the body for shared prediction
        var active = EnsureComp<AugmentReflexBoosterActiveComponent>(performer);
        active.SpeedMultiplier = comp.SpeedMultiplier;
        active.ExpiresAt = _timing.CurTime + comp.Duration;
        Dirty(performer, active);

        _movement.RefreshMovementSpeedModifiers(performer);
        _audio.PlayPvs(comp.ActivateSound, performer);
        _popup.PopupEntity(Loc.GetString("augment-reflex-booster-activate"), performer, performer);

        args.Handled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AugmentReflexBoosterActiveComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ExpiresAt > now)
                continue;

            // Buff expired — remove marker and refresh speed
            RemComp<AugmentReflexBoosterActiveComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);

            // Adrenaline crash: brief knockdown as drawback
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2), true);
            _popup.PopupEntity(Loc.GetString("augment-reflex-booster-crash"), uid, uid);
        }
    }
}
