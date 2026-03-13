// #Misfits Add - Translates Endurance and Strength SPECIAL stats into body resilience buffs.
// Endurance: scales stamina crit threshold up, and slows hunger/thirst decay slightly.
// Strength: scales mob health thresholds up so the player takes more damage before critting.
// All bonuses are intentionally small — at stat 10 each effect is ≈ 4.5%.
// Since stats stack across STR/END/AGL/PER/LCK, individual effects must stay tiny.

using Content.Shared._Misfits.PlayerData.Components;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
/// Applies Endurance and Strength body resilience bonuses on <see cref="SpecialStatsReadyEvent"/>.
/// Endurance scales the stamina crit threshold and reduces hunger/thirst drain rate.
/// Strength scales mob state thresholds so the player absorbs slightly more damage.
/// </summary>
public sealed class SpecialEnduranceSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;

    // END stamina: points added to CritThreshold per END point above 1.
    // END 1: 100 (vanilla), END 10: ~200.
    private const float EnduranceStaminaPerPoint = 11.1f;

    // END hunger/thirst: fractional decay reduction per END point above 1.
    // END 10: 4.5% slower hunger/thirst drain. Intentionally tiny.
    private const float EnduranceDecayReductionPerPoint = 0.005f;

    // STR health: fractional threshold scale per STR point above 1.
    // STR 10: +4.5% effective HP before crit/death. Intentionally tiny.
    private const float StrengthHealthScalePerPoint = 0.005f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersistentPlayerDataComponent, SpecialStatsReadyEvent>(OnStatsReady);
    }

    private void OnStatsReady(Entity<PersistentPlayerDataComponent> ent, ref SpecialStatsReadyEvent args)
    {
        var str = ent.Comp.Strength;
        var end = ent.Comp.Endurance;

        // ── Endurance: stamina pool ───────────────────────────────────────────
        if (TryComp<StaminaComponent>(ent.Owner, out var stamina))
        {
            stamina.CritThreshold = 100f + (end - 1) * EnduranceStaminaPerPoint;
            Dirty(ent.Owner, stamina);
        }

        // ── Endurance: hunger/thirst decay ───────────────────────────────────
        // Reducing BaseDecayRate; HungerSystem/ThirstSystem recalculate ActualDecayRate
        // from BaseDecayRate on their next tick, so no direct system call is needed.
        if (end > 1)
        {
            var decayFactor = 1f - (end - 1) * EnduranceDecayReductionPerPoint;

            if (TryComp<HungerComponent>(ent.Owner, out var hunger))
            {
                hunger.BaseDecayRate *= decayFactor;
                Dirty(ent.Owner, hunger);
            }

            if (TryComp<ThirstComponent>(ent.Owner, out var thirst))
            {
                thirst.BaseDecayRate *= decayFactor;
                Dirty(ent.Owner, thirst);
            }
        }

        // ── Strength: health thresholds ───────────────────────────────────────
        // Scale all mob-state thresholds upward so the player can absorb more
        // damage before going critical or dying. Each threshold is scaled
        // proportionally so the relative gap between crit and dead stays intact.
        if (str <= 1)
            return;

        if (!TryComp<MobThresholdsComponent>(ent.Owner, out var thresholds))
            return;

        var healthScale = 1f + (str - 1) * StrengthHealthScalePerPoint;

        // Iterate over a snapshot to avoid modifying the collection mid-loop.
        foreach (var (baseDamage, mobState) in new Dictionary<FixedPoint2, MobState>(thresholds.Thresholds))
        {
            var scaled = FixedPoint2.New((float) baseDamage * healthScale);
            _mobThresholds.SetMobStateThreshold(ent.Owner, scaled, mobState, thresholds);
        }
    }
}
