// Misfits Change - Plays a short local-area ping when a player or NPC activates combat mode.
// Anti-spam cooldown prevents rapidly toggling the sound by flicking combat mode on and off.
using Content.Shared.CombatMode;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.CombatMode;

/// <summary>
/// Raised on an entity (server-only) when its combat mode transitions from OFF to ON.
/// </summary>
public sealed class CombatModeActivatedEvent : EntityEventArgs { }

/// <summary>
/// Plays a positional ping sound (audible within voice range, ~10 tiles) whenever a
/// non-NPC entity (i.e. a player) activates combat mode via the Num1 toggle.
/// <para>
/// A per-player cooldown blocks the sound from replaying if the player rapidly
/// toggles combat mode on and off.
/// </para>
/// </summary>
public sealed class CombatModePingSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // Seconds a player must wait before the ping can play again after being triggered.
    private const float PingCooldownSeconds = 3f;

    // Voice range in world units — matches SharedChatSystem.VoiceRange (10).
    private const float PingMaxDistance = 10f;

    // The sound that plays on combat mode activation.
    private const string PingSound = "/Audio/Effects/toggleoncombat.ogg";

    // Per-entity timestamp of when the ping last played, used to enforce the cooldown.
    private readonly Dictionary<EntityUid, TimeSpan> _lastPingTime = new();

    public override void Initialize()
    {
        base.Initialize();

        // Misfits Fix - Subscribe to our custom event (raised by CombatModeSystem.SetInCombatMode)
        // instead of (CombatModeComponent, ToggleCombatActionEvent), which is exclusively owned
        // by SharedCombatModeSystem and cannot have a second subscriber.
        SubscribeLocalEvent<CombatModeComponent, CombatModeActivatedEvent>(OnCombatModeActivated);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _lastPingTime.Clear();
    }

    private void OnCombatModeActivated(EntityUid uid, CombatModeComponent comp, CombatModeActivatedEvent args)
    {
        // Enforce anti-spam cooldown so rapid on/off toggling cannot replay the ping.
        var now = _timing.CurTime;
        if (_lastPingTime.TryGetValue(uid, out var lastPing) &&
            (now - lastPing).TotalSeconds < PingCooldownSeconds)
            return;

        _lastPingTime[uid] = now;

        // #Misfits Tweak - NPCs also play the ping when they aggro, not just players.
        // Play positional ping audible within local/voice range (~10 tiles).
        _audio.PlayPvs(
            new SoundPathSpecifier(PingSound),
            uid,
            AudioParams.Default.WithMaxDistance(PingMaxDistance));
    }
}
