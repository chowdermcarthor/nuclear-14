// #Misfits Change: Broadcasts round auto-call deadline to all connected clients.
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared._Misfits.RoundTimer;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.RoundTimer;

/// <summary>
///     Watches <see cref="RoundEndSystem"/> for deadline changes and periodically broadcasts
///     the current auto-call deadline to all clients so they can display a round timer.
/// </summary>
public sealed class RoundTimerBroadcastSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private RoundEndSystem _roundEnd = default!;
    private GameTicker _ticker = default!;

    private TimeSpan _lastBroadcastDeadline;
    private float _rebroadcastTimer;

    /// <summary>Seconds between periodic re-broadcasts (handles late joiners).</summary>
    private const float RebroadcastInterval = 5f;

    public override void Initialize()
    {
        base.Initialize();

        _roundEnd = EntityManager.System<RoundEndSystem>();
        _ticker = EntityManager.System<GameTicker>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _lastBroadcastDeadline = TimeSpan.Zero;
            _rebroadcastTimer = 0;
        });
    }

    private TimeSpan ComputeDeadline()
    {
        int mins = _roundEnd.AutoCalledBefore
            ? _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallExtensionTime)
            : _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallTime);

        if (mins == 0)
            return TimeSpan.Zero;

        return _roundEnd.AutoCallStartTime + TimeSpan.FromMinutes(mins);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.InRound)
        {
            _lastBroadcastDeadline = TimeSpan.Zero;
            return;
        }

        var deadline = ComputeDeadline();

        // Broadcast immediately if deadline changed.
        if (deadline != _lastBroadcastDeadline)
        {
            _lastBroadcastDeadline = deadline;
            RaiseNetworkEvent(new RoundTimerUpdatedEvent(deadline));
            _rebroadcastTimer = 0;
            return;
        }

        // Periodic re-broadcast for late joiners.
        _rebroadcastTimer += frameTime;
        if (_rebroadcastTimer >= RebroadcastInterval)
        {
            _rebroadcastTimer = 0;
            RaiseNetworkEvent(new RoundTimerUpdatedEvent(deadline));
        }
    }
}
