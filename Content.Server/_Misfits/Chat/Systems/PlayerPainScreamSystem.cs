// #Misfits Change - Player-controlled mobs scream on barbed-wire style hazards, bleed start, and severe impacts.
// #Misfits Tweak - Screams now only trigger when the player is near-crit (≥60% of crit threshold).
//                  Cooldown raised to 5 s and hit threshold to 20 to reduce noise on light combat.
using Content.Server.Chat.Systems;
using Content.Server.Damage.Components;
using Content.Server.Damage.Systems;
using Content.Server._Misfits.Chat.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Chat.Systems;

/// <summary>
/// Triggers scream emotes for attached player mobs when they hit painful hazard milestones.
/// Screams on damage/bleed are gated behind a near-crit health check so they don't fire
/// on routine melee hits at full health.
/// </summary>
public sealed class PlayerPainScreamSystem : EntitySystem
{
    private static readonly TimeSpan ScreamCooldown = TimeSpan.FromSeconds(5.0);
    private const float HeavyImpactDamageThreshold = 20f;

    /// <summary>
    /// Fraction of the crit damage threshold that the entity must have accumulated
    /// before damage/bleed screams are allowed.  0.6 = 60 % of the way to crit.
    /// </summary>
    private const float NearCritFraction = 0.6f;

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextScreamTime = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, PlayerDetachedEvent>(OnActorShutdown);
        SubscribeLocalEvent<ActorComponent, DamageChangedEvent>(OnPlayerDamaged);
        SubscribeLocalEvent<ActorComponent, BleedAmountChangedEvent>(OnBleedAmountChanged);
        SubscribeLocalEvent<DamageUserOnTriggerComponent, BeforeDamageUserOnTriggerEvent>(OnBeforeDamageUserOnTrigger);
    }

    private void OnActorShutdown(Entity<ActorComponent> ent, ref PlayerDetachedEvent args)
    {
        _nextScreamTime.Remove(ent);
    }

    private void OnPlayerDamaged(Entity<ActorComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is null)
            return;

        if (args.DamageDelta.GetTotal().Float() < HeavyImpactDamageThreshold)
            return;

        // Only scream on hits when the player is already near-crit — avoids noise on light combat.
        if (!IsNearCrit(ent))
            return;

        TryScream(ent);
    }

    private void OnBleedAmountChanged(Entity<ActorComponent> ent, ref BleedAmountChangedEvent args)
    {
        if (args.PreviousBleedAmount > 0 || args.NewBleedAmount <= 0)
            return;

        // New bleed only warrants a scream when the player is already hurting.
        if (!IsNearCrit(ent))
            return;

        TryScream(ent);
    }

    private void OnBeforeDamageUserOnTrigger(Entity<DamageUserOnTriggerComponent> ent, ref BeforeDamageUserOnTriggerEvent args)
    {
        if (!TryComp<ActorComponent>(args.Tripper, out var actor))
            return;

        if (!IsPainfulHazard(ent))
            return;

        TryScream((args.Tripper, actor));
    }

    /// <summary>
    /// Returns true when the entity's accumulated damage is at or above
    /// <see cref="NearCritFraction"/> of its critical-state damage threshold.
    /// </summary>
    private bool IsNearCrit(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return false;

        if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold)
            || critThreshold is null)
            return false;

        return damageable.TotalDamage.Float() >= critThreshold.Value.Float() * NearCritFraction;
    }

    private bool IsPainfulHazard(Entity<DamageUserOnTriggerComponent> ent)
    {
        var prototypeId = MetaData(ent).EntityPrototype?.ID;
        if (prototypeId == null)
            return false;

        return prototypeId.Contains("Razorwire", StringComparison.OrdinalIgnoreCase)
            || prototypeId.Contains("Barbed", StringComparison.OrdinalIgnoreCase);
    }

    private void TryScream(Entity<ActorComponent> ent)
    {
        if (TryComp<MobStateComponent>(ent, out var mobState) && _mobState.IsDead(ent, mobState))
            return;

        var curTime = _timing.CurTime;
        if (_nextScreamTime.TryGetValue(ent, out var nextTime) && curTime < nextTime)
            return;

        _nextScreamTime[ent] = curTime + ScreamCooldown;
        _chat.TryEmoteWithChat(ent, "Scream");
    }
}