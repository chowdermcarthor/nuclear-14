using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Movement.Systems;
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
        // #Misfits Fix: ordering must match the other InteractUsingEvent subscription in this system.
        SubscribeLocalEvent<PowerArmorIntegrityComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(SharedArmorSystem) });

        // Forward welder interactions on the wearer to the armor entity so
        // RepairableSystem can handle them (InteractUsingEvent is not inventory-relayed).
        SubscribeLocalEvent<PowerArmorWornComponent, InteractUsingEvent>(OnWearerInteractUsing, before: new[] { typeof(SharedArmorSystem) });

        // A suited power armor wearer acts as an immovable wall to other mobs.
        // Cancelling AttemptMobTargetCollideEvent prevents the mob collision system
        // from displacing the wearer when others walk into them.
        SubscribeLocalEvent<PowerArmorWornComponent, AttemptMobTargetCollideEvent>(OnAttemptMobTargetCollide);
    }

    /// <summary>
    ///     Core damage interception. Called after armor coefficients have already
    ///     reduced the incoming damage.
    ///
    ///     While integrity is above zero: <see cref="PowerArmorIntegrityComponent.BleedthroughRatio"/>
    ///     (1.5% by default) bleeds through to the player; the remainder is
    ///     absorbed by the armor's HP pool. When integrity hits zero the armor is
    ///     broken and this handler returns early, letting full damage through.
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

        // Only absorb positive (incoming) damage — don't interfere with healing.
        var incomingDamage = args.Args.Damage;
        if (!incomingDamage.AnyPositive())
            return;

        // Split each damage type: tiny bleedthrough to player, bulk to armor HP.
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

            var toPlayer = amount * comp.BleedthroughRatio;
            var toArmor = amount - toPlayer;

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

        // Only the 1.5% bleedthrough reaches the wearer.
        args.Args.Damage = playerShare;

        // Apply absorbed damage to the armor entity's own DamageableComponent.
        // Server-only to avoid prediction desync; client uses playerShare for prediction.
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
    ///     When power armor is equipped, show the integrity HUD alert on the wearer
    ///     and add a <see cref="PowerArmorWornComponent"/> so welder interactions
    ///     aimed at the player can be forwarded to the armor item.
    /// </summary>
    private void OnEquipped(EntityUid uid, PowerArmorIntegrityComponent comp,
        ref ClothingGotEquippedEvent args)
    {
        UpdateIntegrityAlert(args.Wearer, uid, comp);

        // Track which armor entity the wearer is carrying so we can relay
        // repair interactions to it.
        var worn = EnsureComp<PowerArmorWornComponent>(args.Wearer);
        worn.Armor = uid;
    }

    /// <summary>
    ///     When power armor is unequipped, clear the integrity HUD alert and
    ///     remove the interaction-relay marker from the wearer.
    /// </summary>
    private void OnUnequipped(EntityUid uid, PowerArmorIntegrityComponent comp,
        ref ClothingGotUnequippedEvent args)
    {
        _alerts.ClearAlert(args.Wearer, comp.IntegrityAlert);
        RemCompDeferred<PowerArmorWornComponent>(args.Wearer);
    }

    /// <summary>
    ///     Forwards a welder (InteractUsing) event from the player to their
    ///     worn armor item, allowing a second player to repair the suit while
    ///     it is being worn. Self-repair is still blocked by <see cref="OnInteractUsing"/>.
    /// </summary>
    private void OnWearerInteractUsing(EntityUid uid, PowerArmorWornComponent worn, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!EntityManager.EntityExists(worn.Armor))
            return;

        // Re-raise the event on the armor entity so RepairableSystem can pick it up.
        RaiseLocalEvent(worn.Armor, args);
    }

    /// <summary>
    ///     Prevents other mobs from displacing a power armor wearer via the mob
    ///     collision system. A Paladin blocking a corridor should be an immovable
    ///     wall — others cannot push through or around them by walking into them.
    /// </summary>
    private void OnAttemptMobTargetCollide(EntityUid uid, PowerArmorWornComponent comp, ref AttemptMobTargetCollideEvent args)
    {
        args.Cancelled = true;
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

            // Strip ArmorComponent so broken plating provides no coefficient reduction.
            // Cache the modifiers so they can be restored when the suit is repaired.
            if (TryComp<ArmorComponent>(uid, out var armorComp))
            {
                comp.CachedArmorModifiers = armorComp.Modifiers;
                RemCompDeferred<ArmorComponent>(uid);
            }

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
            // Armor was repaired above 0 — restore ArmorComponent with cached modifiers.
            comp.Broken = false;

            if (comp.CachedArmorModifiers != null)
            {
                var restored = EnsureComp<ArmorComponent>(uid);
                restored.Modifiers = comp.CachedArmorModifiers;
                comp.CachedArmorModifiers = null;
                Dirty(uid, restored);
            }

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

        // Show bleedthrough so players know how much damage reaches them while intact.
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("power-armor-integrity-examine-absorption-header"));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("power-armor-integrity-examine-absorption-value",
            ("value", (int) ((1f - comp.BleedthroughRatio) * 100))));

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
