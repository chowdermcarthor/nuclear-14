// Centralized emote chat rate-limiter with clumping.
// The first emote message from an entity per emote type goes through immediately.
// Subsequent messages of the same type within the cooldown window are suppressed;
// when the window expires a single "(xN)" summary is sent if any were suppressed.
// This prevents chat spam from rapid-fire actions (pointing, shield toggle, etc.)
// without removing the player's ability to perform the action mechanically.
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Chat.Systems;

/// <summary>
/// Provides per-entity, per-emote-type rate limiting with optional clump summaries.
/// All Misfits chat broadcast systems should call <see cref="SendThrottledEmote"/>
/// instead of invoking <see cref="ChatSystem.TrySendInGameICMessage"/> directly.
/// </summary>
public sealed class MisfitsEmoteThrottleSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// How long after the first message before the window resets and a new message is allowed.
    /// </summary>
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Active throttle entries keyed by entity → emote type.
    /// Outer dictionary allows O(1) cleanup on entity removal.
    /// </summary>
    private readonly Dictionary<EntityUid, Dictionary<string, ThrottleEntry>> _active = new();

    /// <summary>
    /// Tracks a throttle window for one (entity, emoteType) pair.
    /// </summary>
    private sealed class ThrottleEntry
    {
        /// <summary>When this window expires and a clump summary can fire.</summary>
        public TimeSpan WindowEnd;

        /// <summary>Number of messages suppressed (not counting the first, which went through).</summary>
        public int SuppressedCount;

        /// <summary>The most recent message text (used for the clump summary).</summary>
        public string LastMessage = string.Empty;

        /// <summary>Whether the source entity was a ghost when the window started.</summary>
        public bool IsGhost;

        /// <summary>Cached player session for ghost-channel routing.</summary>
        public ICommonSession? Session;
    }

    public override void Initialize()
    {
        base.Initialize();

        // Clean up entries when an entity is deleted so the dictionary doesn't leak.
        SubscribeLocalEvent<MetaDataComponent, EntityTerminatingEvent>(OnEntityTerminating);
    }

    /// <summary>
    /// Attempts to send an emote chat message with throttle/clump protection.
    /// <para>
    /// First call in a window: message is sent immediately.<br/>
    /// Subsequent calls within the cooldown: message is silently suppressed and counted.<br/>
    /// When the window expires: if any were suppressed, a single "(xN)" summary is sent.
    /// </para>
    /// </summary>
    /// <param name="source">The entity performing the emote.</param>
    /// <param name="emoteType">A short key identifying the emote type (e.g. "point", "grab").</param>
    /// <param name="message">The localized emote message text.</param>
    /// <param name="range">Chat transmit range (default: Normal).</param>
    /// <returns>True if the message was sent immediately; false if it was suppressed.</returns>
    public bool SendThrottledEmote(
        EntityUid source,
        string emoteType,
        string message,
        ChatTransmitRange range = ChatTransmitRange.Normal)
    {
        if (TerminatingOrDeleted(source))
            return false;

        var now = _timing.CurTime;
        var isGhost = HasComp<GhostComponent>(source);

        // Check for an existing active window.
        if (_active.TryGetValue(source, out var emoteDict) &&
            emoteDict.TryGetValue(emoteType, out var entry) &&
            now < entry.WindowEnd)
        {
            // Within cooldown — suppress this message and bump the counter.
            entry.SuppressedCount++;
            entry.LastMessage = message;
            return false;
        }

        // No active window (or previous window has expired) — send immediately.
        SendEmote(source, message, range, isGhost);

        // Start a new throttle window.
        emoteDict ??= new Dictionary<string, ThrottleEntry>();
        emoteDict[emoteType] = new ThrottleEntry
        {
            WindowEnd = now + DefaultCooldown,
            SuppressedCount = 0,
            LastMessage = message,
            IsGhost = isGhost,
            Session = null,
        };
        _active[source] = emoteDict;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Collect expired entries to avoid mutation during iteration.
        List<(EntityUid Entity, string EmoteType)>? expired = null;

        foreach (var (entity, emoteDict) in _active)
        {
            foreach (var (emoteType, entry) in emoteDict)
            {
                if (now < entry.WindowEnd)
                    continue;

                // Window expired — send a clump summary if any messages were suppressed.
                if (entry.SuppressedCount > 0 && !TerminatingOrDeleted(entity))
                {
                    var clumpMessage = Loc.GetString("misfits-emote-clump",
                        ("message", entry.LastMessage),
                        ("count", entry.SuppressedCount));

                    SendEmote(entity, clumpMessage, ChatTransmitRange.Normal, entry.IsGhost);
                }

                expired ??= new List<(EntityUid, string)>();
                expired.Add((entity, emoteType));
            }
        }

        if (expired == null)
            return;

        foreach (var (entity, emoteType) in expired)
        {
            if (!_active.TryGetValue(entity, out var dict))
                continue;

            dict.Remove(emoteType);
            if (dict.Count == 0)
                _active.Remove(entity);
        }
    }

    /// <summary>
    /// Routes the emote message to the correct chat channel based on ghost state.
    /// </summary>
    private void SendEmote(EntityUid source, string message, ChatTransmitRange range, bool isGhost)
    {
        if (isGhost)
        {
            if (_playerManager.TryGetSessionByEntity(source, out var session))
                _chat.TrySendInGameOOCMessage(source, message, InGameOOCChatType.Dead, false, player: session);
        }
        else
        {
            _chat.TrySendInGameICMessage(source, message, InGameICChatType.Emote,
                range, ignoreActionBlocker: true);
        }
    }

    private void OnEntityTerminating(EntityUid uid, MetaDataComponent comp, ref EntityTerminatingEvent args)
    {
        _active.Remove(uid);
    }
}
