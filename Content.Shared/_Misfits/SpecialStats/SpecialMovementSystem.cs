// #Misfits Add - Translates Agility and Strength SPECIAL stats into movement modifiers.
// • Agility: scales walk/sprint speed linearly above the baseline.
//   AGL 1 = no change, AGL 10 ≈ +13.5% speed.
// • Strength: while actively wielding a weapon, proportionally offsets the
//   wield-slowdown penalty applied by WieldableSystem.
//   STR 1 = penalty stays intact, STR 10 = penalty fully negated.
// Both effects are contributed through the standard RefreshMovementSpeedModifiersEvent
// so they stack correctly with all other speed modifiers in the game.

using Content.Shared._Misfits.PlayerData.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Misfits.SpecialStats;

/// <summary>
/// Applies Agility and Strength SPECIAL stat bonuses as movement speed modifiers.
/// Subscribes to <see cref="RefreshMovementSpeedModifiersEvent"/> on any entity
/// that owns a <see cref="PersistentPlayerDataComponent"/>.
/// </summary>
public sealed class SpecialMovementSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    // Agility: +1.5% speed per point above 1 (max +13.5% at AGL 10)
    private const float AgilitySpeedPerPoint = 0.015f;

    // The standard wield-slowdown magnitude: 1 − DefaultWeaponWieldedSpeedModifier = 1 − 0.85 = 0.15
    // Used to compute how much counter-multiplier STR provides.
    private const float WieldPenaltyMagnitude = 1f - WieldableComponent.DefaultWeaponWieldedSpeedModifier;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PersistentPlayerDataComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<PersistentPlayerDataComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var str = ent.Comp.Strength;
        var agl = ent.Comp.Agility;

        // ── Agility: unconditional walk/sprint bonus ──────────────────────────
        // Scales linearly: each point above 1 adds AgilitySpeedPerPoint to the multiplier.
        if (agl > 1)
        {
            var agilityMod = 1f + (agl - 1) * AgilitySpeedPerPoint;
            args.ModifySpeed(agilityMod, agilityMod);
        }

        // ── Strength: proportionally counter the wield-slowdown penalty ───────
        // WieldableSystem applies a 0.85× multiplier to the holder when a weapon is wielded.
        // STR provides an opposing multiplier that scales toward fully negating that penalty.
        // Only active when the player is currently wielding a weapon with a slowdown.
        if (str <= 1)
            return;

        if (!_hands.TryGetActiveItem(ent.Owner, out var heldItem))
            return;

        if (!TryComp<WieldableComponent>(heldItem!.Value, out var wieldable) || !wieldable.Wielded)
            return;

        // Only provide a counter when the held item actually applies a slowdown.
        // WieldableSystem uses a custom modifier or falls back to the default for guns/melees.
        var hasCustomModifier = wieldable.WieldedSpeedModifier != null;
        var isWeapon = HasComp<GunComponent>(heldItem.Value) || HasComp<MeleeWeaponComponent>(heldItem.Value);
        if (!hasCustomModifier && !isWeapon)
            return;

        // counter = 1 / (1 − penalty × (STR−1)/9)
        // STR  1 → 1.0   (no counter)
        // STR  5 → ~1.071 (partial)
        // STR 10 → ~1.176 (fully negates the default 0.85× penalty)
        var counter = 1f / (1f - WieldPenaltyMagnitude * (str - 1) / 9f);
        args.ModifySpeed(counter, counter);
    }
}
