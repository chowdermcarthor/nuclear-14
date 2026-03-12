// #Misfits Add: broadcast local emotes for thrown impacts and poison-bearing thrown items.
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Ensnaring.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Content.Shared.FixedPoint;

namespace Content.Server._Misfits.Combat;

public sealed class ThrowImpactChatSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateComponent, ThrowHitByEvent>(OnThrowHitBy);
    }

    private void OnThrowHitBy(EntityUid uid, MobStateComponent component, ThrowHitByEvent args)
    {
        if (args.User is not { } user)
            return;

        // Dedicated ensnare chat handles bolas and other thrown ensnaring tools.
        if (TryComp<EnsnaringComponent>(args.Thrown, out var ensnaring) && ensnaring.CanThrowTrigger)
            return;

        var targetName = Identity.Entity(uid, EntityManager);
        var itemName = Identity.Entity(args.Thrown, EntityManager);
        var userName = Identity.Entity(user, EntityManager);

        if (TryComp<DamageOtherOnHitComponent>(args.Thrown, out var damage) && IsPoisonous(damage))
        {
            _chat.TrySendInGameICMessage(user,
                Loc.GetString("misfits-chat-throw-poison-hit", ("target", targetName), ("item", itemName)),
                InGameICChatType.Emote,
                ChatTransmitRange.Normal,
                ignoreActionBlocker: true);

            _chat.TrySendInGameICMessage(uid,
                Loc.GetString("misfits-chat-throw-poison-hit-victim", ("user", userName), ("item", itemName)),
                InGameICChatType.Emote,
                ChatTransmitRange.Normal,
                ignoreActionBlocker: true);
            return;
        }

        _chat.TrySendInGameICMessage(user,
            Loc.GetString("misfits-chat-throw-hit", ("target", targetName), ("item", itemName)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);

        _chat.TrySendInGameICMessage(uid,
            Loc.GetString("misfits-chat-throw-hit-victim", ("user", userName), ("item", itemName)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            ignoreActionBlocker: true);
    }

    private static bool IsPoisonous(DamageOtherOnHitComponent component)
    {
        return component.Damage.DamageDict.TryGetValue("Poison", out FixedPoint2 poisonDamage)
               && poisonDamage > FixedPoint2.Zero;
    }
}