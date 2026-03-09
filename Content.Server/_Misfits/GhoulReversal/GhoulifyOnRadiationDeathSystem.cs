// #Misfits Change
using Content.Server._Misfits.GhoulReversal;
using Content.Server.Ghoul;
using Content.Server.Humanoid;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Misfits.GhoulReversal;

/// <summary>
/// When a humanoid with GhoulifyOnRadiationDeathComponent dies to radiation damage,
/// they transform into the Ghoul player species and are revived at low health,
/// instead of fully dying. The Promethine chemistry reagent can reverse this
/// within the first 12 real hours.
/// </summary>
public sealed class GhoulifyOnRadiationDeathSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhoulifyOnRadiationDeathComponent, MobStateChangedEvent>(OnMobStateDeath);
    }

    private void OnMobStateDeath(EntityUid uid, GhoulifyOnRadiationDeathComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Must be a humanoid player character
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
            return;

        // Don't re-ghoulify someone already a ghoul or super mutant
        if (appearance.Species == "Ghoul" || appearance.Species == "GhoulGlowing" || appearance.Species == "SuperMutant")
            return;

        // Check that a meaningful amount of radiation damage was accumulated
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var radDamage = 0f;
        if (damageable.Damage.DamageDict.TryGetValue("Radiation", out var rad))
            radDamage = rad.Float();

        if (radDamage < component.MinimumRadiationDamage)
            return;

        // --- Transform into Ghoul player species ---
        _humanoid.SetSpecies(uid, component.GhoulSpecies);

        // Revive the player: temporarily allow revives, heal all damage, then re-lock.
        if (TryComp<MobThresholdsComponent>(uid, out var thresholds))
        {
            _mobThreshold.SetAllowRevives(uid, true, thresholds);
            _damageable.SetAllDamage(uid, damageable, FixedPoint2.Zero);
            _mobThreshold.SetAllowRevives(uid, false, thresholds);
        }
        else
        {
            _damageable.SetAllDamage(uid, damageable, FixedPoint2.Zero);
        }

        // Stamp the time component so Promethine chemistry can gatekeep reversal
        EnsureComp<GhoulificationTimeComponent>(uid);

        // Add feral tracker — further radiation can still push them to feral
        EnsureComp<FeralGhoulifyComponent>(uid);

        _popup.PopupEntity(
            Loc.GetString("ghoulify-on-death-self"),
            uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(
            Loc.GetString("ghoulify-on-death-others", ("target", uid)),
            uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
    }
}
