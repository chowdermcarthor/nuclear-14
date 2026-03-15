// #Misfits Add: Broadcasts emote chat messages to nearby players when a player raises or lowers their shield.
// Replaces the observer-visible PopupEntity calls that were commented out of Content.Shared/Blocking/BlockingSystem.cs.
// Chat messages are throttled via MisfitsEmoteThrottleSystem to prevent spam from rapid shield toggling.
using Content.Server._Misfits.Chat.Systems;
using Content.Shared.Blocking;

namespace Content.Server._Misfits.Combat;

/// <summary>
/// Hooks explicit shield blocking toggle events to send local-area emote chat messages so nearby players
/// see shield raise/lower text in the emote channel instead of observer popup text.
/// </summary>
public sealed class BlockingChatSystem : EntitySystem
{
    [Dependency] private readonly MisfitsEmoteThrottleSystem _throttle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlockingComponent, ShieldBlockingStartedEvent>(OnBlockingStarted);
        SubscribeLocalEvent<BlockingComponent, ShieldBlockingStoppedEvent>(OnBlockingStopped);
    }

    private void OnBlockingStarted(EntityUid uid, BlockingComponent comp, ref ShieldBlockingStartedEvent args)
    {
        // Resolve the shield's display name; fall back to a generic string if the item is gone.
        var shieldName = Exists(args.Item)
            ? Name(args.Item)
            : "shield";

        var message = Loc.GetString("misfits-chat-blocking-start", ("shield", shieldName));

        // Throttle system sends the first message immediately and clumps repeats.
        _throttle.SendThrottledEmote(args.User, "block", message);
    }

    private void OnBlockingStopped(EntityUid uid, BlockingComponent comp, ref ShieldBlockingStoppedEvent args)
    {
        // Skip if the entity itself is being deleted; sending chat from a terminating entity causes errors.
        if (TerminatingOrDeleted(args.User))
            return;

        var shieldName = Exists(args.Item)
            ? Name(args.Item)
            : "shield";

        var message = Loc.GetString("misfits-chat-blocking-stop", ("shield", shieldName));

        // Throttle system sends the first message immediately and clumps repeats.
        _throttle.SendThrottledEmote(args.User, "block", message);
    }
}
