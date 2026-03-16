using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.PowerArmor;

/// <summary>
///     Intercepts damage flowing through worn power armor and splits it between
///     the armor's own HP pool and the wearer. Runs AFTER <see cref="SharedArmorSystem"/>
///     so that armor coefficients have already reduced the incoming damage before
///     this system applies absorption.
///
///     Flow: raw damage → ArmorComponent coefficients → IntegritySystem absorption split
///       → portion to armor HP (via DamageableComponent on the item)
///       → remainder to the wearer
///
///     As the armor accumulates damage its effective absorption ratio degrades
///     through configurable tiers. When fully broken (0 integrity), all damage
///     passes through to the wearer. Repair with a welder restores integrity.
/// </summary>
public sealed class PowerArmorIntegritySystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Run after SharedArmorSystem so coefficients apply before we split damage.
        SubscribeLocalEvent<PowerArmorIntegrityComponent, InventoryRelayedEvent<DamageModifyEvent>>(
            OnDamageModify, after: new[] { typeof(SharedArmorSystem) });

        // Track broken / repaired state whenever the armor item's own HP changes.
        SubscribeLocalEvent<PowerArmorIntegrityComponent, DamageChangedEvent>(OnArmorDamageChanged);

        // Let players examine the armor's current integrity.
        SubscribeLocalEvent<PowerArmorIntegrityComponent, GetVerbsEvent<ExamineVerb>>(OnExamine);

        // Show/clear HUD alert when armor is equipped or removed.
        SubscribeLocalEvent<PowerArmorIntegrityComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<PowerArmorIntegrityComponent, ClothingGotUnequippedEvent>(OnUnequipped);

        // Block self-repair: can't weld your own suit while wearing it.
        SubscribeLocalEvent<PowerArmorIntegrityComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(SharedArmorSystem) });
    }

    /// <summary>
    ///     Core damage interception. Called after armor coefficients have already
    ///     reduced the incoming damage. Splits the remaining damage between the
    ///     armor's integrity pool and the wearer.
    /// </summary>
    private void OnDamageModify(EntityUid uid, PowerArmorIntegrityComponent comp,
        InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (comp.Broken)
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var integrity = GetIntegrity(comp, damageable);
        if (integrity <= 0)
            return;

        var effectiveAbsorption = GetEffectiveAbsorption(comp, integrity);
        if (effectiveAbsorption <= 0f)
            return;

        // Only absorb positive (incoming) damage — don't interfere with healing.
        var incomingDamage = args.Args.Damage;
        if (!incomingDamage.AnyPositive())
            return;

        // Penetration scaling: heavy-calibre rounds overwhelm the plating.
        // Total positive damage is compared against the penetration threshold;
        // above it, absorption is linearly reduced toward zero at the cap.
        var totalPositive = 0f;
        foreach (var (_, amount) in incomingDamage.DamageDict)
        {
            if (amount > 0)
                totalPositive += (float) amount;
        }

        if (totalPositive > comp.PenetrationThreshold && comp.PenetrationCap > comp.PenetrationThreshold)
        {
            var penetrationFactor = Math.Clamp(
                (totalPositive - comp.PenetrationThreshold) / (comp.PenetrationCap - comp.PenetrationThreshold),
                0f, 1f);

            // Scale absorption down — a round at the cap punches clean through.
            effectiveAbsorption *= 1f - penetrationFactor;
        }

        if (effectiveAbsorption <= 0f)
            return;

        // Split each damage type: armorShare goes to armor HP, rest to wearer.
        var armorShare = new DamageSpecifier();
        var playerShare = new DamageSpecifier();

        foreach (var (type, amount) in incomingDamage.DamageDict)
        {
            if (amount <= 0)
            {
                // Negative values (healing) always go to the player, never to armor.
                playerShare.DamageDict[type] = amount;
                continue;
            }

            var toArmor = amount * effectiveAbsorption;
            var toPlayer = amount - toArmor;

            // Don't let armor absorb more than its remaining integrity (total, not per-type).
            // This prevents over-absorption on the killing blow.
            if (toArmor > integrity)
            {
                toPlayer += toArmor - integrity;
                toArmor = integrity;
            }

            armorShare.DamageDict[type] = toArmor;
            playerShare.DamageDict[type] = toPlayer;
        }

        // What reaches the wearer is only the player's share.
        args.Args.Damage = playerShare;

        // Apply absorbed damage to the armor entity's own DamageableComponent.
        // Server-only to avoid prediction desync; client reduces wearer damage for prediction.
        if (_net.IsServer && armorShare.AnyPositive())
        {
            _damageable.TryChangeDamage(uid, armorShare, ignoreResistances: true, interruptsDoAfters: false);
        }
    }

    /// <summary>
    ///     Prevents the wearer from repairing their own power armor while wearing it.
    ///     Must exit the suit or have another player weld it.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, PowerArmorIntegrityComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only care if the armor is inside a container (i.e. worn).
        if (!_container.TryGetContainingContainer((uid, null, null), out var container))
            return;

        // If the person trying to repair is the one wearing the armor, block it.
        if (container.Owner != args.User)
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            _popup.PopupEntity(
                Loc.GetString("power-armor-integrity-no-self-repair"),
                args.User, args.User, PopupType.MediumCaution);
        }
    }

    /// <summary>
    ///     When power armor is equipped, show the integrity HUD alert on the wearer.
    /// </summary>
    private void OnEquipped(EntityUid uid, PowerArmorIntegrityComponent comp,
        ref ClothingGotEquippedEvent args)
    {
        UpdateIntegrityAlert(args.Wearer, uid, comp);
    }

    /// <summary>
    ///     When power armor is unequipped, clear the integrity HUD alert.
    /// </summary>
    private void OnUnequipped(EntityUid uid, PowerArmorIntegrityComponent comp,
        ref ClothingGotUnequippedEvent args)
    {
        _alerts.ClearAlert(args.Wearer, comp.IntegrityAlert);
    }

    /// <summary>
    ///     Fires when the armor item's own DamageableComponent changes (from
    ///     absorbing hits or being repaired). Updates the broken flag and notifies
    ///     the wearer.
    /// </summary>
    private void OnArmorDamageChanged(EntityUid uid, PowerArmorIntegrityComponent comp,
        DamageChangedEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var integrity = GetIntegrity(comp, damageable);
        var wasBroken = comp.Broken;

        if (integrity <= 0 && !wasBroken)
        {
            comp.Broken = true;
            Dirty(uid, comp);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("power-armor-integrity-broken", ("armor", uid)),
                    uid, PopupType.LargeCaution);
            }
        }
        else if (integrity > 0 && wasBroken)
        {
            // Armor was repaired above 0.
            comp.Broken = false;
            Dirty(uid, comp);

            if (_net.IsServer)
            {
                _popup.PopupEntity(
                    Loc.GetString("power-armor-integrity-restored", ("armor", uid)),
                    uid, PopupType.Medium);
            }
        }

        // Refresh the wearer's HUD alert to reflect current integrity.
        if (_container.TryGetContainingContainer((uid, null, null), out var container))
            UpdateIntegrityAlert(container.Owner, uid, comp);
    }

    /// <summary>
    ///     Adds an examine verb showing current integrity and degradation state.
    /// </summary>
    private void OnExamine(EntityUid uid, PowerArmorIntegrityComponent comp,
        GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var integrity = GetIntegrity(comp, damageable);
        var fraction = (float) integrity / (float) comp.MaxIntegrity;

        var msg = new FormattedMessage();

        if (comp.Broken)
        {
            msg.AddMarkupOrThrow(Loc.GetString("power-armor-integrity-examine-broken"));
        }
        else
        {
            // Color-code by integrity fraction.
            var color = fraction > 0.66f ? "green" : fraction > 0.33f ? "yellow" : "red";
            msg.AddMarkupOrThrow(Loc.GetString("power-armor-integrity-examine",
                ("current", (int) integrity),
                ("max", (int) comp.MaxIntegrity),
                ("color", color)));
        }

        _examine.AddDetailedExamineVerb(args, comp, msg,
            Loc.GetString("power-armor-integrity-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("power-armor-integrity-verb-message"));
    }

    /// <summary>
    ///     Current remaining integrity = max minus accumulated damage.
    /// </summary>
    private FixedPoint2 GetIntegrity(PowerArmorIntegrityComponent comp, DamageableComponent damageable)
    {
        return FixedPoint2.Max(comp.MaxIntegrity - damageable.TotalDamage, 0);
    }

    /// <summary>
    ///     Returns the effective absorption ratio based on the armor's current
    ///     integrity fraction and its degradation tiers.
    /// </summary>
    private float GetEffectiveAbsorption(PowerArmorIntegrityComponent comp, FixedPoint2 integrity)
    {
        var fraction = (float) integrity / (float) comp.MaxIntegrity;

        // Walk tiers from highest threshold down. Each tier we fall below
        // overwrites the result; stop on the first tier we're above.
        var effective = comp.AbsorptionRatio;
        foreach (var tier in comp.DegradationTiers)
        {
            if (fraction <= tier.Threshold)
                effective = tier.Absorption;
            else
                break;
        }

        return effective;
    }

    /// <summary>
    ///     Updates (or sets) the wearer's HUD alert to reflect current armor
    ///     integrity. Higher severity = more health remaining.
    /// </summary>
    private void UpdateIntegrityAlert(EntityUid wearer, EntityUid armorUid,
        PowerArmorIntegrityComponent comp)
    {
        if (!TryComp<DamageableComponent>(armorUid, out var damageable))
            return;

        var integrity = GetIntegrity(comp, damageable);
        var severity = (short) ContentHelpers.RoundToLevels(
            (double) integrity,
            (double) comp.MaxIntegrity,
            comp.AlertLevels);

        _alerts.ShowAlert(wearer, comp.IntegrityAlert, severity);
    }
}
