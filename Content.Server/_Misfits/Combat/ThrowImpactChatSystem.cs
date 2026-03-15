// #Misfits Add: broadcast local emotes for thrown impacts and poison-bearing thrown items.
// Chat messages are throttled via MisfitsEmoteThrottleSystem to prevent spam from rapid throws.
using Content.Server._Misfits.Chat.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Ensnaring.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Combat;

public sealed class ThrowImpactChatSystem : EntitySystem
{
    [Dependency] private readonly MisfitsEmoteThrottleSystem _throttle = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    // #Misfits Fix: deduplicate messages when a thrown item has multiple fixtures
    // that each fire StartCollideEvent for the same (user, thrown, target) pair in one tick.
    private readonly HashSet<(EntityUid User, EntityUid Thrown, EntityUid Target)> _processedThisTick = new();
    private GameTick _lastCleanupTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateComponent, ThrowHitByEvent>(OnThrowHitBy);
    }

    private void OnThrowHitBy(EntityUid uid, MobStateComponent component, ThrowHitByEvent args)
    {
        if (args.User is not { } user)
            return;

        // Clear dedup set once per tick so it never grows unbounded.
        var curTick = _gameTiming.CurTick;
        if (curTick != _lastCleanupTick)
        {
            _processedThisTick.Clear();
            _lastCleanupTick = curTick;
        }

        // Skip if we already sent messages for this exact (user, thrown, target) this tick.
        var key = (user, args.Thrown, uid);
        if (!_processedThisTick.Add(key))
            return;

        // Dedicated ensnare chat handles bolas and other thrown ensnaring tools.
        if (TryComp<EnsnaringComponent>(args.Thrown, out var ensnaring) && ensnaring.CanThrowTrigger)
            return;

        var targetName = Identity.Entity(uid, EntityManager);
        var itemName = Identity.Entity(args.Thrown, EntityManager);

        if (TryComp<DamageOtherOnHitComponent>(args.Thrown, out var damage) && IsPoisonous(damage))
        {
            _throttle.SendThrottledEmote(user, "throw",
                Loc.GetString("misfits-chat-throw-poison-hit", ("target", targetName), ("item", itemName)));
            // #Misfits Fix: removed redundant victim emote — performer's emote already
            // communicates the hit to everyone nearby.
            return;
        }

        _throttle.SendThrottledEmote(user, "throw",
            Loc.GetString("misfits-chat-throw-hit", ("target", targetName), ("item", itemName)));
        // #Misfits Fix: removed redundant victim emote — performer's emote already
        // communicates the hit to everyone nearby.
    }

    private static bool IsPoisonous(DamageOtherOnHitComponent component)
    {
        return component.Damage.DamageDict.TryGetValue("Poison", out FixedPoint2 poisonDamage)
               && poisonDamage > FixedPoint2.Zero;
    }
}