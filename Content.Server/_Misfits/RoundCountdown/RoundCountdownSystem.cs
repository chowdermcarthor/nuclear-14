// #Misfits Change: Countdown announcements and automatic round-decision vote.
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.RoundCountdown;

/// <summary>
///     Announces remaining time before the emergency shuttle auto-call and triggers an
///     automatic Yes/No extend-round vote at the 15-minute mark.
/// </summary>
public sealed class RoundCountdownSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private RoundEndSystem _roundEnd = default!;
    private GameTicker _ticker = default!;
    private IVoteManager _voteManager = default!;

    private bool _announced60;
    private bool _announced30;
    private bool _announced15;
    private bool _voteTriggered;

    /// <summary>Track AutoCallStartTime so we can detect resets (extensions).</summary>
    private TimeSpan _lastAutoCallStart;

    public override void Initialize()
    {
        base.Initialize();

        _roundEnd = EntityManager.System<RoundEndSystem>();
        _ticker = EntityManager.System<GameTicker>();
        _voteManager = IoCManager.Resolve<IVoteManager>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
    }

    private void Reset()
    {
        _announced60 = false;
        _announced30 = false;
        _announced15 = false;
        _voteTriggered = false;
        _lastAutoCallStart = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        // If the auto-call timer was reset (round extended / shuttle recalled), clear flags.
        if (_roundEnd.AutoCallStartTime != _lastAutoCallStart)
        {
            _lastAutoCallStart = _roundEnd.AutoCallStartTime;
            _announced60 = false;
            _announced30 = false;
            _announced15 = false;
            _voteTriggered = false;
        }

        var autoCallMins = _roundEnd.AutoCalledBefore
            ? _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallExtensionTime)
            : _cfg.GetCVar(CCVars.EmergencyShuttleAutoCallTime);

        // Auto-call disabled.
        if (autoCallMins == 0)
            return;

        var elapsed = _gameTiming.CurTime - _roundEnd.AutoCallStartTime;
        var remaining = TimeSpan.FromMinutes(autoCallMins) - elapsed;

        // 60-minute warning (only fires if the auto-call window is longer than 60 min).
        if (!_announced60 && remaining <= TimeSpan.FromMinutes(60) && remaining > TimeSpan.FromMinutes(30))
        {
            _announced60 = true;
            _chatManager.DispatchServerAnnouncement(
                Loc.GetString("ui-round-countdown-60"));
        }

        // 30-minute warning.
        if (!_announced30 && remaining <= TimeSpan.FromMinutes(30) && remaining > TimeSpan.FromMinutes(15))
        {
            _announced30 = true;
            _chatManager.DispatchServerAnnouncement(
                Loc.GetString("ui-round-countdown-30"));
        }

        // 15-minute warning + automatic vote.
        if (!_announced15 && remaining <= TimeSpan.FromMinutes(15))
        {
            _announced15 = true;
            _chatManager.DispatchServerAnnouncement(
                Loc.GetString("ui-round-countdown-15"));

            if (!_voteTriggered)
            {
                _voteTriggered = true;
                TriggerRoundDecisionVote();
            }
        }
    }

    private void TriggerRoundDecisionVote()
    {
        // Don't start a vote if one is already running.
        if (_voteManager.ActiveVotes.Any())
            return;

        var options = new VoteOptions
        {
            Title = Loc.GetString("ui-vote-round-decision-title"),
            InitiatorText = Loc.GetString("ui-vote-initiator-server"),
            Options =
            {
                (Loc.GetString("ui-vote-round-decision-yes"), "yes"),
                (Loc.GetString("ui-vote-round-decision-no"), "no"),
            },
            Duration = TimeSpan.FromSeconds(90),
        };

        var vote = _voteManager.CreateVote(options);

        vote.OnFinished += (_, _) =>
        {
            var votesYes = vote.VotesPerOption["yes"];
            var votesNo = vote.VotesPerOption["no"];
            var totalConnected = _playerManager.Sessions
                .Count(s => s.Status != SessionStatus.Disconnected);

            if (votesYes > votesNo)
            {
                _chatManager.DispatchServerAnnouncement(
                    Loc.GetString("ui-vote-round-decision-yes-won",
                        ("yesVotes", votesYes), ("noVotes", votesNo), ("total", totalConnected)));
                _roundEnd.ExtendRound();
            }
            else if (votesNo > votesYes)
            {
                _chatManager.DispatchServerAnnouncement(
                    Loc.GetString("ui-vote-round-decision-no-won",
                        ("noVotes", votesNo), ("yesVotes", votesYes), ("total", totalConnected)));
                _roundEnd.RequestRoundEnd(null, false);
            }
            else
            {
                // Tie — default to extending the round.
                _chatManager.DispatchServerAnnouncement(
                    Loc.GetString("ui-vote-round-decision-tie",
                        ("yesVotes", votesYes), ("noVotes", votesNo), ("total", totalConnected)));
                _roundEnd.ExtendRound();
            }
        };
    }
}
