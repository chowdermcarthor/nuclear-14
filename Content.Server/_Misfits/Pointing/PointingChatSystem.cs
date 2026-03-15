// #Misfits Add: Broadcasts an emote chat message to nearby players when a player points at an entity.
// Ghosts are routed to Dead chat instead of the IC emote channel.
// Chat messages are throttled via MisfitsEmoteThrottleSystem to prevent rapid-fire spam;
// the visual pointing arrow still fires at the normal 0.5 s rate.
using Content.Server._Misfits.Chat.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Pointing;

namespace Content.Server._Misfits.Pointing;

/// <summary>
/// Hooks <see cref="AfterPointedAtEvent"/> to send a local-area emote chat message so nearby
/// players see "* John Smith points at Jane Doe *" in the emote channel whenever someone points.
/// Ghosts are routed to Dead chat so other ghosts can see the gesture.
/// Repeated messages within the throttle window are clumped into a single "(xN)" summary.
/// </summary>
public sealed class PointingChatSystem : EntitySystem
{
    [Dependency] private readonly MisfitsEmoteThrottleSystem _throttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to the event raised on the pointer entity after a successful point.
        SubscribeLocalEvent<MetaDataComponent, AfterPointedAtEvent>(OnAfterPointed);
    }

    /// <summary>
    /// Sends a chat message when a player points at another entity.
    /// The throttle system handles ghost → Dead chat routing and clump summaries.
    /// </summary>
    private void OnAfterPointed(EntityUid uid, MetaDataComponent component, ref AfterPointedAtEvent ev)
    {
        // Use a dedicated reflexive string for self-pointing so the chat line reads
        // "<name> points at themselves" instead of formatting the actor as a target name.
        var message = uid == ev.Pointed
            ? Loc.GetString("pointing-chat-point-at-self")
            : Loc.GetString("pointing-chat-point-at-other", ("other", Identity.Entity(ev.Pointed, EntityManager)));

        // Throttle system sends the first message immediately and clumps repeats.
        _throttle.SendThrottledEmote(uid, "point", message);
    }
}
