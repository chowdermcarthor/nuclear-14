// #Misfits Add — Smoke Signal server system.
// Allows any Tribe-department player to activate a signal fire, type a short message,
// and broadcast it to all online Tribe-department players as a styled popup + chat notice.
//
// Flow:
//   1. Player right-clicks a SmokeSignalComponent entity → verb appears if they are in Tribe department.
//   2. Server opens the BUI (text-input window) on the activator's session.
//   3. Player types message and confirms → SmokeSignalSendMessage arrives.
//   4. Server validates cooldown, clamps message, records cooldown end time.
//   5. Message is broadcast as a styled announcement pop-up to every living Tribe-dept player.

using Content.Shared._Misfits.SmokeSignal;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player; // #Misfits Fix - ActorComponent lives in Robust.Shared.Player
using Robust.Shared.Timing;

namespace Content.Server._Misfits.SmokeSignal;

/// <summary>
/// Handles the smoke signal activation verb, BUI messaging, cooldown management, and department broadcast.
/// </summary>
public sealed class SmokeSignalSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Register the activation verb on any entity with SmokeSignalComponent
        SubscribeLocalEvent<SmokeSignalComponent, GetVerbsEvent<ActivationVerb>>(OnGetVerb);

        // Handle message sent from the BUI text input
        SubscribeLocalEvent<SmokeSignalComponent, SmokeSignalSendMessage>(OnSendMessage);
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Verb: "Send Smoke Signal"
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnGetVerb(EntityUid uid, SmokeSignalComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Only Tribe-department players see this verb
        if (!IsInDepartment(args.User, component.TargetDepartment))
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("smoke-signal-verb"),
            Act = () =>
            {
                // Check cooldown before opening the UI
                if (component.CooldownEnd.HasValue && _timing.CurTime < component.CooldownEnd.Value)
                {
                    var remaining = (int) Math.Ceiling((component.CooldownEnd.Value - _timing.CurTime).TotalSeconds);
                    _popup.PopupEntity(
                        Loc.GetString("smoke-signal-cooldown", ("seconds", remaining)),
                        uid, args.User, PopupType.SmallCaution);
                    return;
                }

                // Open the text input window for the activating player
                _ui.OpenUi(uid, SmokeSignalUiKey.Key, args.User);
            },
            Priority = 1,
        });
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  BUI message handler: validate and broadcast
    // ──────────────────────────────────────────────────────────────────────────────────

    private void OnSendMessage(EntityUid uid, SmokeSignalComponent component, SmokeSignalSendMessage args)
    {
        if (args.Actor is not { Valid: true } sender) // #Misfits Fix - .Session removed from BUI messages; use args.Actor
            return;

        // Re-validate cooldown (race guard)
        if (component.CooldownEnd.HasValue && _timing.CurTime < component.CooldownEnd.Value)
            return;

        // Clamp and sanitize message
        var message = args.Message.Trim();
        if (message.Length == 0)
            return;

        if (message.Length > component.MaxMessageLength)
            message = message[..component.MaxMessageLength];

        // Record cooldown
        component.CooldownEnd = _timing.CurTime + component.Cooldown;

        // Build the broadcast text
        var broadcastText = Loc.GetString("smoke-signal-broadcast", ("message", message));

        // Send a large popup to every living Tribe-department player
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var playerUid, out _))
        {
            if (_mobState.IsDead(playerUid))
                continue;

            if (!IsInDepartment(playerUid, component.TargetDepartment))
                continue;

            _popup.PopupEntity(broadcastText, playerUid, playerUid, PopupType.Large);
        }

        // Log so admins can see in the server log
        Log.Info($"[SmokeSignal] {ToPrettyString(sender)} sent: {message}");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the entity's job belongs to the given department.
    /// Mirrors the TribalHuntSystem.IsInDepartment pattern exactly.
    /// </summary>
    private bool IsInDepartment(EntityUid uid, string departmentId)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var jobPrototype))
            return false;

        return _jobs.TryGetDepartment(jobPrototype.ID, out var department) && department.ID == departmentId;
    }
}
