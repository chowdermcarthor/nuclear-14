// #Misfits Add - Manages DrugSpecialBoostComponent lifecycle (expiry) and translates
// drug-sourced SPECIAL stat deltas into tangible in-game effects:
//   • Perception → gun spread / recoil reduction  (stacks with SpecialPerceptionSystem base)
//   • Agility    → walk/sprint speed bonus         (stacks with SpecialMovementSystem base)
//   • Strength   → melee damage bonus              (stacks with SpecialMovementSystem wield-counter)
// Component expiry is polled every update; on removal speed modifiers are refreshed.

using Angle = Robust.Shared.Maths.Angle;
using Content.Shared._Misfits.PlayerData.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
/// Handles the lifecycle of <see cref="DrugSpecialBoostComponent"/> and converts
/// its active stat deltas into gameplay effects via event subscriptions.
/// <list type="bullet">
///   <item>Perception boost → gun spread/recoil reduction (identical rate to SpecialPerceptionSystem)</item>
///   <item>Agility boost    → walk/sprint speed multiplier (identical rate to SpecialMovementSystem)</item>
///   <item>Strength boost   → melee damage bonus (+4 % per point)</item>
/// </list>
/// </summary>
public sealed class DrugSpecialBoostSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;

    // ── Constants matching existing base-SPECIAL systems so stacking scales uniformly ──

    /// <summary>Spread/recoil reduction per PER point (mirrors SpecialPerceptionSystem).</summary>
    private const float PerceptionReductionPerPoint = 0.005f;

    /// <summary>Walk/sprint speed bonus per AGI point (mirrors SpecialMovementSystem).</summary>
    private const float AgilitySpeedBonusPerPoint = 0.015f;

    /// <summary>Bonus melee damage fraction per STR point (e.g. STR +2 → +8 % damage).</summary>
    private const float StrengthDamageBonusPerPoint = 0.04f;

    // Maintained across frames for O(n) expiry checking without per-tick querying.
    private readonly List<Entity<DrugSpecialBoostComponent>> _tracked = new();

    public override void Initialize()
    {
        base.Initialize();

        // Update runs even between predicted ticks so the timer expires reliably.
        UpdatesOutsidePrediction = true;

        // Track each new component so Update() can iterate without a full ECS query.
        SubscribeLocalEvent<DrugSpecialBoostComponent, ComponentStartup>(OnStartup);

        // ── Per-frame event hooks ───────────────────────────────────────────────
        // Agility: modify movement speed whenever the engine recalculates it.
        SubscribeLocalEvent<DrugSpecialBoostComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);

        // Perception: tighten gun spread/recoil for every shot fired by the holder.
        // Global subscription — we identify the holder by walking the transform parent.
        SubscribeLocalEvent<GunRefreshModifiersEvent>(OnGunRefresh);

        // Strength: add bonus melee damage on every hit.
        // Global subscription — we check the User field directly.
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
    }

    // ── Startup tracking ────────────────────────────────────────────────────────

    private void OnStartup(Entity<DrugSpecialBoostComponent> ent, ref ComponentStartup args)
    {
        _tracked.Add(ent);
    }

    // ── Agility → movement speed ───────────────────────────────────────────────

    private void OnRefreshSpeed(EntityUid uid, DrugSpecialBoostComponent comp, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.AgilityBoost <= 0)
            return;

        // Each AGI point above 0 gives AgilitySpeedBonusPerPoint boost.
        // This delta stacks on top of SpecialMovementSystem's base AGI contribution.
        var bonus = 1f + comp.AgilityBoost * AgilitySpeedBonusPerPoint;
        args.ModifySpeed(bonus, bonus);
    }

    // ── Perception → gun spread / recoil ───────────────────────────────────────

    private void OnGunRefresh(ref GunRefreshModifiersEvent args)
    {
        // Walk up the transform hierarchy to find the entity holding the gun.
        var holder = Transform(args.Gun.Owner).ParentUid;

        if (!TryComp<DrugSpecialBoostComponent>(holder, out var comp) || comp.PerceptionBoost <= 0)
            return;

        // Same formula as SpecialPerceptionSystem — each PER point narrows spread by 0.5 %.
        var reduction    = comp.PerceptionBoost * PerceptionReductionPerPoint;
        var keepFraction = 1.0 - reduction;

        args.MinAngle          = new Angle((double) args.MinAngle          * keepFraction);
        args.MaxAngle          = new Angle((double) args.MaxAngle          * keepFraction);
        args.AngleIncrease     = new Angle((double) args.AngleIncrease     * keepFraction);
        args.CameraRecoilScalar *= (float) keepFraction;
    }

    // ── Strength → melee damage ────────────────────────────────────────────────

    private void OnMeleeHit(MeleeHitEvent args)
    {
        // Only process actual hits (not examination/preview calls).
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!TryComp<DrugSpecialBoostComponent>(args.User, out var comp) || comp.StrengthBoost <= 0)
            return;

        // Each STR point contributes StrengthDamageBonusPerPoint extra damage.
        // Bonus damage is proportional to the weapon's base damage types so any
        // weapon benefits without needing weapon-specific handling.
        var bonusFraction = comp.StrengthBoost * StrengthDamageBonusPerPoint;
        args.BonusDamage += args.BaseDamage * bonusFraction;
    }

    // ── Timer / expiry ──────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        for (var i = _tracked.Count - 1; i >= 0; i--)
        {
            var ent = _tracked[i];

            // Purge stale references to entities that were deleted externally.
            if (ent.Comp.Deleted)
            {
                _tracked.RemoveAt(i);
                continue;
            }

            // Not yet expired — keep checking next frame.
            if (ent.Comp.ExpireTime > now)
                continue;

            _tracked.RemoveAt(i);

            // Agility boost was active — force a speed recalculation on expiry
            // so the player is not stuck with the bonus after the drug wears off.
            if (ent.Comp.AgilityBoost != 0)
                _speedModifier.RefreshMovementSpeedModifiers(ent.Owner);

            RemComp<DrugSpecialBoostComponent>(ent.Owner);
        }
    }

    // ── Public API (called by SpecialStatBoostEffect each metabolism tick) ──────

    /// <summary>
    /// Pushes the expiry window forward by <paramref name="lifetimeSeconds"/> seconds
    /// relative to the current time (or the existing timer, whichever is later).
    /// This keeps the boost alive as long as the drug is still being metabolised.
    /// </summary>
    public void RefreshTimer(EntityUid uid, DrugSpecialBoostComponent comp, float lifetimeSeconds)
    {
        // Take the later of 'now' or the current timer so we never shorten an existing window.
        var baseSeconds = Math.Max(comp.ExpireTime.TotalSeconds, _timing.CurTime.TotalSeconds);
        comp.ExpireTime = TimeSpan.FromSeconds(baseSeconds + lifetimeSeconds);
        Dirty(uid, comp);
    }
}
