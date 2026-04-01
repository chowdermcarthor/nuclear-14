// Server-side emote + power drain handler for Assaultron beam charge-up events.
// Shot blocking / state machine logic lives in SharedAssaultronBeamChargeSystem
// (Content.Shared) so it runs on both client and server for proper prediction.
// This system only exists server-side because ChatSystem and BatterySystem are server-only.

using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Misfits.Robot;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Robot;

public sealed class AssaultronBeamChargeEmoteSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly BatterySystem _battery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AssaultronBeamChargeComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<AssaultronBeamChargeComponent, AssaultronChargeStartedEvent>(OnChargeStarted);
        SubscribeLocalEvent<AssaultronBeamChargeComponent, AssaultronBeamFiredEvent>(OnBeamFired);
    }

    /// <summary>
    /// Blocks the shot server-side if the chassis power cell has less charge than
    /// FireDrainCharge. The shared system handles charge-up/cooldown gating; this
    /// handler enforces the battery-depletion condition that shared code cannot check
    /// (BatteryComponent is server-only).
    /// </summary>
    private void OnAttemptShoot(EntityUid uid, AssaultronBeamChargeComponent comp, ref AttemptShootEvent args)
    {
        // Already cancelled by the shared charge/cooldown gate — nothing to add.
        if (args.Cancelled || comp.FireDrainCharge <= 0f)
            return;

        // Still in the charge-up phase — shared system blocks the shot; skip until ready.
        if (comp.IsCharging)
            return;

        var cellEntity = _itemSlots.GetItemOrNull(uid, comp.CellSlotId);
        if (cellEntity == null || !TryComp<BatteryComponent>(cellEntity.Value, out var battery))
        {
            args.Cancelled = true;
            return;
        }

        // Require at least FireDrainCharge available before allowing the shot.
        if (battery.CurrentCharge < comp.FireDrainCharge)
            args.Cancelled = true;
    }

    private void OnChargeStarted(EntityUid uid, AssaultronBeamChargeComponent comp, ref AssaultronChargeStartedEvent args)
    {
        var now = _timing.CurTime;
        if (now < comp.NextChargeEmoteTime)
            return;

        comp.NextChargeEmoteTime = now + TimeSpan.FromSeconds(comp.EmoteCooldown);

        _chat.TrySendInGameICMessage(
            uid,
            Loc.GetString(args.EmoteLocale),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }

    private void OnBeamFired(EntityUid uid, AssaultronBeamChargeComponent comp, ref AssaultronBeamFiredEvent args)
    {
        // Drain the robot's chassis battery on each shot.
        if (comp.FireDrainCharge > 0f)
            DrainCellSlot(uid, comp);

        var now = _timing.CurTime;
        if (now < comp.NextFireEmoteTime)
            return;

        comp.NextFireEmoteTime = now + TimeSpan.FromSeconds(comp.EmoteCooldown);

        _chat.TrySendInGameICMessage(
            uid,
            Loc.GetString(args.EmoteLocale),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }

    /// <summary>
    /// Drains charge from the battery entity stored in the robot's cell_slot.
    /// This makes the beam attack consume the robot's own power supply.
    /// </summary>
    private void DrainCellSlot(EntityUid uid, AssaultronBeamChargeComponent comp)
    {
        var cellEntity = _itemSlots.GetItemOrNull(uid, comp.CellSlotId);
        if (cellEntity == null)
            return;

        _battery.UseCharge(cellEntity.Value, comp.FireDrainCharge);
    }
}
