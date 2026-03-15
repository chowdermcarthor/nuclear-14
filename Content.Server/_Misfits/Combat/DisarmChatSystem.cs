// #Misfits Add: Broadcasts a chat emote when a player successfully disarms and knocks another player down.
// Replaces the observer-visible PopupEntity call commented out of Content.Shared/Damage/Systems/StaminaSystem.cs.
// Chat messages are throttled via MisfitsEmoteThrottleSystem to prevent spam from rapid disarms.
using Content.Server._Misfits.Chat.Systems;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;

namespace Content.Server._Misfits.Combat;

/// <summary>
/// Hooks <see cref="DisarmedEvent"/> on <see cref="StaminaComponent"/> to send a local-area emote
/// chat message to nearby players when a disarm attempt knocks a target down (enters stam-crit).
/// <para>
/// The shared <see cref="Content.Shared.Damage.Systems.StaminaSystem"/> sets
/// <see cref="HandledEntityEventArgs.Handled"/> to true only when the full knockdown path runs,
/// so we use that flag to avoid firing on non-critical disarms.
/// </para>
/// </summary>
public sealed class DisarmChatSystem : EntitySystem
{
    [Dependency] private readonly MisfitsEmoteThrottleSystem _throttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe AFTER the shared StaminaSystem, which sets args.Handled = true on full knockdown.
        // Uses MobStateComponent instead of StaminaComponent to avoid duplicate subscription conflict.
        SubscribeLocalEvent<MobStateComponent, DisarmedEvent>(OnDisarmed);
    }

    private void OnDisarmed(EntityUid uid, MobStateComponent component, DisarmedEvent args)
    {
        // SharedStaminaSystem sets Handled = true only when the target enters stam-crit from this disarm.
        // If it's false here, the disarm didn't result in a knockdown — nothing to broadcast.
        if (!args.Handled)
            return;

        // Resolve identity-masked display name of the target.
        var targetName = Identity.Entity(args.Target, EntityManager);
        var message = Loc.GetString("misfits-chat-disarm-knockdown", ("target", targetName));

        // Throttle system sends the first message immediately and clumps repeats.
        _throttle.SendThrottledEmote(args.Source, "disarm", message);
    }
}
