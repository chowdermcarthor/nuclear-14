// #Misfits Add: Broadcasts a chat emote and plays a grab sound when a mob entity is pulled/grabbed by another entity.
// Chat messages are throttled via MisfitsEmoteThrottleSystem to prevent spam from rapid grab-release toggling.
using Content.Server._Misfits.Chat.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Misfits.Pulling;

/// <summary>
/// Hooks <see cref="PullStartedMessage"/> to broadcast a local-area emote chat message and
/// play a fabric-grab sound whenever a mob entity is grabbed/pulled by another entity.
/// Only fires when the pulled entity is a mob (has <see cref="MobStateComponent"/>) — dragging
/// crates or other objects will not produce the emote.
/// </summary>
public sealed class GrabChatSystem : EntitySystem
{
    [Dependency] private readonly MisfitsEmoteThrottleSystem _throttle = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    // Sound played at the puller's position when a grab starts — a cloth rustle
    // to represent seizing someone's arm / collar.
    private static readonly SoundPathSpecifier GrabSound =
        new SoundPathSpecifier("/Audio/Items/Handling/cloth_pickup.ogg");

    public override void Initialize()
    {
        base.Initialize();

        // PullStartedMessage is raised on the pullable entity; we only care when
        // the thing being pulled is an actual mob (player, NPC, etc.).
        SubscribeLocalEvent<PullableComponent, PullStartedMessage>(OnPullStarted);
    }

    /// <summary>
    /// Fired on the pullable entity when a pull begins.
    /// Sends an emote line from the puller and plays a grab sound.
    /// </summary>
    private void OnPullStarted(EntityUid uid, PullableComponent component, PullStartedMessage args)
    {
        // Only fire for mob entities — skip items, machinery, corpse-less objects.
        if (!HasComp<MobStateComponent>(uid))
            return;

        // Don't fire if the puller and pullable are the same entity.
        if (args.PullerUid == uid)
            return;

        // Build the emote message. Identity.Entity respects disguises/name masks.
        var pulledName = Identity.Entity(uid, EntityManager);
        var message = Loc.GetString("misfits-chat-grab-start", ("grabbed", pulledName));

        // Throttle system sends the first message immediately and clumps repeats.
        _throttle.SendThrottledEmote(args.PullerUid, "grab", message);

        // Play a short cloth/fabric rustle at the puller to reinforce the physical action.
        _audio.PlayPvs(GrabSound, args.PullerUid);
    }
}
