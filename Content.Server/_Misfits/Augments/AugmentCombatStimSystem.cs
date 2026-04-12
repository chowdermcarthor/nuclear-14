// #Misfits Add - Server system for Vault-Tec Combat Stim Injector augment.
// Handles activation, melee damage modifier, damage resistance, timer, and toxin penalty.

using Content.Shared._Misfits.Augments;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Augments;

/// <summary>
/// Server-side activation, damage modifiers, and timer management for the combat stim augment.
/// Boosts melee damage and applies damage resistance while active,
/// then deals Poison damage when the buff expires.
/// </summary>
public sealed class AugmentCombatStimSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Action event fires on the implant entity (action container)
        SubscribeLocalEvent<AugmentCombatStimComponent, ActivateCombatStimEvent>(OnActivate);

        // Melee damage boost (broadcast on weapon, check args.User)
        SubscribeLocalEvent<GetMeleeDamageEvent>(OnGetMeleeDamage);

        // Damage resistance (directed on damaged entity)
        SubscribeLocalEvent<AugmentCombatStimActiveComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnActivate(EntityUid uid, AugmentCombatStimComponent comp,
        ActivateCombatStimEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        // Can't stack activations
        if (HasComp<AugmentCombatStimActiveComponent>(performer))
            return;

        // Place active marker on the body
        var active = EnsureComp<AugmentCombatStimActiveComponent>(performer);
        active.MeleeDamageMultiplier = comp.MeleeDamageMultiplier;
        active.DamageResistanceSet = comp.DamageResistanceSet;
        active.ExpiresAt = _timing.CurTime + comp.Duration;
        Dirty(performer, active);

        _audio.PlayPvs(comp.ActivateSound, performer);
        _popup.PopupEntity(Loc.GetString("augment-combat-stim-activate"), performer, performer);

        args.Handled = true;
    }

    private void OnGetMeleeDamage(ref GetMeleeDamageEvent args)
    {
        if (!TryComp<AugmentCombatStimActiveComponent>(args.User, out var comp))
            return;

        // Guard: don't apply if expired (edge case during removal frame)
        if (comp.ExpiresAt != TimeSpan.Zero && comp.ExpiresAt <= _timing.CurTime)
            return;

        args.Damage *= comp.MeleeDamageMultiplier;
    }

    private void OnDamageModify(EntityUid uid, AugmentCombatStimActiveComponent comp,
        DamageModifyEvent args)
    {
        // Guard: don't apply if expired
        if (comp.ExpiresAt != TimeSpan.Zero && comp.ExpiresAt <= _timing.CurTime)
            return;

        if (!_proto.TryIndex(comp.DamageResistanceSet, out var modifierSet))
            return;

        // Apply damage resistance coefficients
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifierSet);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AugmentCombatStimActiveComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ExpiresAt > now)
                continue;

            // Buff expired — remove marker
            RemComp<AugmentCombatStimActiveComponent>(uid);

            // Toxin penalty: deal Poison damage as the crash
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Poison", FixedPoint2.New(25f));
            _damageable.TryChangeDamage(uid, damage, ignoreResistances: true);

            _popup.PopupEntity(Loc.GetString("augment-combat-stim-crash"), uid, uid);
        }
    }
}
