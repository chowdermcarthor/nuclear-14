// #Misfits Change Add - Escalates a repeated grab into a choking carry with a fixed speed penalty.
// Phases: Pending (10 s wind-up, broken by victim movement) → Active (carry + suffocation → forced crit at 30 s).
// Stack-overflow fix: ComponentShutdown handlers guard on LifeStage >= Stopping to break mutual-recursion.
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Carrying;
using Content.Server.Chat.Systems;
using Content.Server._Misfits.Grabbing.Components;
using Content.Shared.Chat;
using Content.Shared._Misfits.Movement.Pulling.Events;
using Content.Shared.Carrying;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Movement.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Grabbing;

public sealed class DoubleGrabSystem : EntitySystem
{
    [Dependency] private readonly CarryingSystem _carrying = default!;
    [Dependency] private readonly CarryingSlowdownSystem _carryingSlowdown = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly RespiratorSystem _respirator = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RepeatPullAttemptEvent>(OnRepeatPullAttempt);
        SubscribeLocalEvent<BeingDoubleGrabbedComponent, MoveInputEvent>(OnVictimMoveInput);
        SubscribeLocalEvent<DoubleGrabComponent, ComponentShutdown>(OnGrabberShutdown);
        SubscribeLocalEvent<BeingDoubleGrabbedComponent, ComponentShutdown>(OnVictimShutdown);
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void OnRepeatPullAttempt(ref RepeatPullAttemptEvent args)
    {
        // Absorb repeated attempts while a wind-up is already in progress for this pair.
        if (TryComp<DoubleGrabComponent>(args.User, out var existing) && existing.Victim == args.Target)
        {
            args.Handled = true;
            return;
        }

        if (HasComp<DoubleGrabComponent>(args.User) ||
            HasComp<BeingCarriedComponent>(args.User) ||
            !HasComp<CarriableComponent>(args.Target))
        {
            return;
        }

        // Only allow double-grabbing entities with an active mind (i.e. player-controlled).
        // Prevents choking NPCs, animals, and any AI-controlled mob.
        if (!TryComp<MindContainerComponent>(args.Target, out var mind) || !mind.HasMind)
            return;

        StartDoubleGrab(args.User, args.Target);
        args.Handled = true;
    }

    /// <summary>
    /// Victim pressing a movement key during the Pending wind-up cancels the grab.
    /// During Active carry the engine blocks free movement, so this only fires meaningfully in Pending.
    /// </summary>
    private void OnVictimMoveInput(Entity<BeingDoubleGrabbedComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (!TryComp<DoubleGrabComponent>(ent.Comp.Carrier, out var grabComp) ||
            grabComp.Phase != DoubleGrabPhase.Pending)
            return;

        _chat.TrySendInGameICMessage(ent.Owner,
            Loc.GetString("misfits-chat-double-grab-resist", ("carrier", Identity.Entity(ent.Comp.Carrier, EntityManager))),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);

        StopDoubleGrab(ent.Comp.Carrier, ent.Owner);
    }

    // ── Shutdown Guards (prevent mutual-recursion / stack overflow) ───────────

    private void OnGrabberShutdown(Entity<DoubleGrabComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<BeingDoubleGrabbedComponent>(ent.Comp.Victim, out var victimComp) ||
            victimComp.Carrier != ent.Owner ||
            victimComp.LifeStage >= ComponentLifeStage.Stopping) // already being removed — break the cycle
            return;

        RemComp<BeingDoubleGrabbedComponent>(ent.Comp.Victim);
    }

    private void OnVictimShutdown(Entity<BeingDoubleGrabbedComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<DoubleGrabComponent>(ent.Comp.Carrier, out var grabComp) ||
            grabComp.Victim != ent.Owner ||
            grabComp.LifeStage >= ComponentLifeStage.Stopping) // already being removed — break the cycle
            return;

        RemComp<DoubleGrabComponent>(ent.Comp.Carrier);
    }

    // ── Per-tick Update ───────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DoubleGrabComponent>();
        while (query.MoveNext(out var carrier, out var grab))
        {
            switch (grab.Phase)
            {
                case DoubleGrabPhase.Pending:
                    UpdatePending(carrier, grab, frameTime);
                    break;
                case DoubleGrabPhase.Active:
                    UpdateActive(carrier, grab, frameTime);
                    break;
            }
        }
    }

    private void UpdatePending(EntityUid carrier, DoubleGrabComponent grab, float frameTime)
    {
        // Abort if victim component is gone, pairing is stale, pull was dropped, or someone died.
        if (!TryComp<BeingDoubleGrabbedComponent>(grab.Victim, out var victimComp) ||
            victimComp.Carrier != carrier ||
            !TryComp<PullerComponent>(carrier, out var puller) ||
            puller.Pulling != grab.Victim ||
            _mobState.IsDead(carrier) ||
            _mobState.IsDead(grab.Victim))
        {
            StopDoubleGrab(carrier, grab.Victim);
            return;
        }

        grab.HeldTime += TimeSpan.FromSeconds(frameTime);
        if (grab.HeldTime < grab.PinTime)
            return;

        TransitionToActive(carrier, grab);
    }

    private void TransitionToActive(EntityUid carrier, DoubleGrabComponent grab)
    {
        var victim = grab.Victim;

        if (!_carrying.TryCarry(carrier, victim))
        {
            StopDoubleGrab(carrier, victim);
            return;
        }

        grab.Phase = DoubleGrabPhase.Active;
        grab.HeldTime = TimeSpan.Zero;
        grab.CritApplied = false;

        if (TryComp<BeingDoubleGrabbedComponent>(victim, out var victimComp))
            victimComp.NextGaspEmoteTime = _gameTiming.CurTime;

        _chat.TrySendInGameICMessage(carrier,
            Loc.GetString("misfits-chat-double-grab-cinch", ("victim", Identity.Entity(victim, EntityManager))),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
        // #Misfits Fix: removed redundant victim emote — performer's "pins {victim} in a firm grip"
        // already communicates the action to everyone nearby.

        if (TryComp<CarryingSlowdownComponent>(carrier, out var slowdown))
            _carryingSlowdown.SetModifier(carrier, grab.CarrySpeedModifier, grab.CarrySpeedModifier, slowdown);
    }

    private void UpdateActive(EntityUid carrier, DoubleGrabComponent grab, float frameTime)
    {
        var victim = grab.Victim;

        // Abort if the carry was broken externally (escaped, teleported, etc.).
        if (!TryComp<CarryingComponent>(carrier, out var carrying) ||
            carrying.Carried != victim ||
            !TryComp<BeingDoubleGrabbedComponent>(victim, out var victimComp) ||
            victimComp.Carrier != carrier ||
            !TryComp<BeingCarriedComponent>(victim, out var beingCarried) ||
            beingCarried.Carrier != carrier)
        {
            // Don't call RecalculateCarrySlowdown — carry is already gone.
            StopDoubleGrab(carrier, victim, restoreCarrySlowdown: false);
            return;
        }

        if (_mobState.IsDead(victim) || _mobState.IsDead(carrier))
        {
            StopDoubleGrab(carrier, victim);
            return;
        }

        grab.HeldTime += TimeSpan.FromSeconds(frameTime);

        // Oxygen drain after SuffocationStartTime.
        if (grab.HeldTime >= grab.SuffocationStartTime &&
            TryComp<RespiratorComponent>(victim, out var respirator))
        {
            _respirator.UpdateSaturation(victim, -frameTime * grab.SuffocationDrainPerSecond, respirator);

            if (respirator.Saturation < respirator.SuffocationThreshold &&
                _gameTiming.CurTime >= victimComp.NextGaspEmoteTime)
            {
                victimComp.NextGaspEmoteTime = _gameTiming.CurTime + victimComp.GaspEmoteCooldown;
                _chat.TrySendInGameICMessage(victim,
                    Loc.GetString("misfits-chat-double-grab-gasp"),
                    InGameICChatType.Emote,
                    ChatTransmitRange.Normal,
                    ignoreActionBlocker: true);
            }
        }

        // Force critical state at CritTime.
        if (!grab.CritApplied && grab.HeldTime >= grab.CritTime)
        {
            ForceVictimCritical(carrier, victim);
            grab.CritApplied = true;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StartDoubleGrab(EntityUid carrier, EntityUid victim)
    {
        var grabComp = EnsureComp<DoubleGrabComponent>(carrier);
        grabComp.Victim = victim;
        grabComp.Phase = DoubleGrabPhase.Pending;
        grabComp.HeldTime = TimeSpan.Zero;

        var victimComp = EnsureComp<BeingDoubleGrabbedComponent>(victim);
        victimComp.Carrier = carrier;
    }

    /// <summary>
    /// Single clean-up entry point. Removes both components idempotently.
    /// The ComponentShutdown handlers use LifeStage guards to prevent mutual recursion.
    /// </summary>
    private void StopDoubleGrab(EntityUid carrier, EntityUid victim, bool restoreCarrySlowdown = true)
    {
        // Capture whether we were in active phase before removal.
        var wasActive = TryComp<DoubleGrabComponent>(carrier, out var grabComp) &&
                        grabComp.Victim == victim &&
                        grabComp.Phase == DoubleGrabPhase.Active;

        if (TryComp<BeingDoubleGrabbedComponent>(victim, out var victimComp) && victimComp.Carrier == carrier)
            RemComp<BeingDoubleGrabbedComponent>(victim);

        // OnVictimShutdown may have already cascaded removal of DoubleGrabComponent.
        if (TryComp<DoubleGrabComponent>(carrier, out grabComp) && grabComp.Victim == victim)
            RemComp<DoubleGrabComponent>(carrier);

        if (wasActive && restoreCarrySlowdown &&
            TryComp<CarryingComponent>(carrier, out var carrying) &&
            carrying.Carried == victim)
        {
            _carrying.RecalculateCarrySlowdown(carrier, victim);
        }
    }

    private void ForceVictimCritical(EntityUid carrier, EntityUid victim)
    {
        if (!_mobThreshold.TryGetThresholdForState(victim, MobState.Critical, out var critThreshold) ||
            !TryComp<DamageableComponent>(victim, out var damageable) ||
            !TryComp<MobStateComponent>(victim, out var mobState))
        {
            return;
        }

        if (mobState.CurrentState == MobState.Critical || mobState.CurrentState == MobState.Dead)
            return;

        var remainingDamage = critThreshold.Value - damageable.TotalDamage;
        if (remainingDamage <= FixedPoint2.Zero)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Asphyxiation"] = remainingDamage + FixedPoint2.New(1);
        _damageable.TryChangeDamage(victim, damage, origin: carrier, partMultiplier: 0f);
    }
}