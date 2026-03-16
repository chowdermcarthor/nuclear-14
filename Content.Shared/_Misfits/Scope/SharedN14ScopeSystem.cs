using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;


namespace Content.Shared._Misfits.Scope;

/// <summary>
/// Action event fired when the player presses the "Toggle Scope" hotbar button.
/// Handled by <see cref="SharedN14ScopeSystem"/>.
/// </summary>
public sealed partial class N14ScopeToggleEvent : InstantActionEvent { }

/// <summary>
/// Shared base for the N14 scope system.
///
/// Two activation modes:
///  1. Built-in scope  — <see cref="N14ScopeComponent"/> is on the gun entity itself.
///     Action is granted when the gun is picked up into a hand.
///  2. Attachment scope — <see cref="N14ScopeComponent"/> is on a standalone item
///     inserted into the gun's "gun_scope" ItemSlot.
///     Action is granted when the hosting gun is picked up.
///
/// Client override applies/resets eye offset; server stub inherits empty defaults.
/// </summary>
public abstract class SharedN14ScopeSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;

    /// <summary>ItemSlots slot key used for the removable scope attachment.</summary>
    public const string ScopeSlotId = "gun_scope";

    public override void Initialize()
    {
        base.Initialize();

        // Built-in scope on a gun entity
        SubscribeLocalEvent<N14ScopeComponent, GotEquippedHandEvent>(OnScopeEquipped);
        SubscribeLocalEvent<N14ScopeComponent, GotUnequippedHandEvent>(OnScopeUnequipped);
        SubscribeLocalEvent<N14ScopeComponent, ComponentShutdown>(OnScopeShutdown);

        // Attachment scope: check gun_scope slot when any gun with ItemSlots is equipped
        SubscribeLocalEvent<ItemSlotsComponent, GotEquippedHandEvent>(OnGunWithSlotEquipped);
        SubscribeLocalEvent<ItemSlotsComponent, GotUnequippedHandEvent>(OnGunWithSlotUnequipped);

        // Toggle action pressed by player
        SubscribeLocalEvent<N14ScopeToggleEvent>(OnScopeToggle);
    }

    // ── Built-in scope (N14ScopeComponent directly on the gun) ────────────────

    private void OnScopeEquipped(Entity<N14ScopeComponent> ent, ref GotEquippedHandEvent args)
    {
        // Standalone attachment items are slotted into guns — not held directly.
        if (ent.Comp.IsAttachment)
            return;

        GrantScopeAction(ent, ent, args.User);
    }

    private void OnScopeUnequipped(Entity<N14ScopeComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (!ent.Comp.IsAttachment)
            CleanupScope(ent, args.User);
    }

    private void OnScopeShutdown(Entity<N14ScopeComponent> ent, ref ComponentShutdown args)
    {
        CleanupScope(ent, ent.Comp.CurrentUser);
    }

    // ── Attachment scope (scope item in gun's gun_scope ItemSlot) ─────────────

    private void OnGunWithSlotEquipped(Entity<ItemSlotsComponent> gun, ref GotEquippedHandEvent args)
    {
        // If the gun itself has a built-in scope, OnScopeEquipped handles it.
        if (HasComp<N14ScopeComponent>(gun))
            return;

        if (!TryGetScopeInSlot(gun, out var scope))
            return;

        // Container for the action is the gun, so the action is auto-revoked when gun is dropped.
        GrantScopeAction(scope, gun, args.User);
    }

    private void OnGunWithSlotUnequipped(Entity<ItemSlotsComponent> gun, ref GotUnequippedHandEvent args)
    {
        if (HasComp<N14ScopeComponent>(gun))
            return;

        if (!TryGetScopeInSlot(gun, out var scope))
            return;

        CleanupScope(scope, args.User);
    }

    // ── Toggle action handler ──────────────────────────────────────────────────

    private void OnScopeToggle(N14ScopeToggleEvent args)
    {
        var user = args.Performer;

        if (!TryGetHeldScope(user, out var scope))
            return;

        scope.Comp.IsActive = !scope.Comp.IsActive;
        Dirty(scope);

        if (scope.Comp.IsActive)
            OnScopeActivated(scope, user);
        else
            OnScopeDeactivated(scope, user);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void GrantScopeAction(Entity<N14ScopeComponent> scope, EntityUid container, EntityUid user)
    {
        scope.Comp.CurrentUser = user;
        Actions.AddAction(user, ref scope.Comp.ToggleActionEntity, scope.Comp.ToggleActionId, container);
        Dirty(scope);
    }

    private void CleanupScope(Entity<N14ScopeComponent> scope, EntityUid? user)
    {
        if (scope.Comp.IsActive)
        {
            scope.Comp.IsActive = false;
            if (user.HasValue)
                OnScopeDeactivated(scope, user.Value);
        }

        if (scope.Comp.ToggleActionEntity.HasValue)
        {
            Actions.RemoveAction(scope.Comp.ToggleActionEntity.Value);
            scope.Comp.ToggleActionEntity = null;
        }

        scope.Comp.CurrentUser = null;
        Dirty(scope);
    }

    /// <summary>
    /// Finds a scope attachment item in the gun's scope slot.
    /// </summary>
    private bool TryGetScopeInSlot(Entity<ItemSlotsComponent> gun, out Entity<N14ScopeComponent> scope)
    {
        scope = default;

        if (!_slots.TryGetSlot(gun, ScopeSlotId, out var slot, gun.Comp))
            return false;

        if (slot.Item is not { } slotItem || !TryComp<N14ScopeComponent>(slotItem, out var comp))
            return false;

        scope = (slotItem, comp);
        return true;
    }

    /// <summary>
    /// Returns the active scope entity for the given user, checking:
    ///  1. The actively held item has <see cref="N14ScopeComponent"/> (built-in).
    ///  2. The actively held item has a scope attachment in its gun_scope slot.
    /// </summary>
    protected bool TryGetHeldScope(EntityUid user, out Entity<N14ScopeComponent> scope)
    {
        scope = default;

        if (!TryComp<HandsComponent>(user, out var hands) || hands.ActiveHandEntity is not { } held)
            return false;

        // Case 1: built-in scope on the held item
        if (TryComp<N14ScopeComponent>(held, out var builtIn))
        {
            scope = (held, builtIn);
            return true;
        }

        // Case 2: scope attachment in the gun's scope slot
        if (!TryComp<ItemSlotsComponent>(held, out var slots))
            return false;

        if (!_slots.TryGetSlot(held, ScopeSlotId, out var slot, slots))
            return false;

        if (slot.Item is not { } slotItem || !TryComp<N14ScopeComponent>(slotItem, out var attached))
            return false;

        scope = (slotItem, attached);
        return true;
    }

    // ── Virtual hooks for client/server overrides ─────────────────────────────

    /// <summary>Called when the scope is toggled ON. Client override sets eye offset.</summary>
    protected virtual void OnScopeActivated(Entity<N14ScopeComponent> scope, EntityUid user) { }

    /// <summary>Called when the scope is toggled OFF or the gun is dropped. Client override resets eye offset.</summary>
    protected virtual void OnScopeDeactivated(Entity<N14ScopeComponent> scope, EntityUid user) { }
}
