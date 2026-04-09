using System.Collections.Generic;
using Content.Server.Actions;
using Content.Shared._Misfits.TribalHunt;
using Content.Shared._Misfits.Warcry;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Simple tribal hunt flow: chief starts hunt, tribe joins for 2 minutes via GUI,
/// then a legendary Deathclaw is spawned and tracked until it is killed.
/// </summary>
public sealed class TribalHuntSystem : EntitySystem
{
    private enum TribalHuntStage : byte
    {
        Inactive,
        Gathering,
        Active,
    }

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LegendaryCreatureSpawnerSystem _legendarySpawner = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private TribalHuntStage _stage = TribalHuntStage.Inactive;
    private TimeSpan _gatheringEndsAt;
    private TimeSpan _huntEndsAt;
    private TimeSpan _configuredHuntDuration;
    private TimeSpan _configuredRewardDuration;
    private float _configuredRewardSpeedBonus;
    private EntityUid? _activeLegendaryCreature;
    private EntityUid? _activeHuntSessionId;
    private EntityUid? _chief;
    private string _targetDepartment = "Tribe";
    private readonly HashSet<EntityUid> _joinedHunters = new();
    private TimeSpan _lastLocationBroadcast;
    private TimeSpan _lastUiHeartbeat;
    private string _lastKnownCoordinates = string.Empty;
    private bool _hasKnownCoordinates;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TribalHuntLeaderComponent, ComponentStartup>(OnLeaderStartup);
        SubscribeLocalEvent<TribalHuntLeaderComponent, ComponentShutdown>(OnLeaderShutdown);
        SubscribeLocalEvent<TribalHuntLeaderComponent, PerformTribalStartHuntActionEvent>(OnStartHuntAction);

        SubscribeLocalEvent<TribalHuntParticipantComponent, ComponentStartup>(OnParticipantStartup);
        SubscribeLocalEvent<TribalHuntParticipantComponent, ComponentShutdown>(OnParticipantShutdown);
        SubscribeLocalEvent<TribalHuntParticipantComponent, PerformTribalToggleHuntGuiActionEvent>(OnToggleHuntGuiAction);

        SubscribeLocalEvent<LegendaryCreatureComponent, LegendaryCreatureKilledEvent>(OnLegendaryCreatureKilled);
        SubscribeLocalEvent<LegendaryCreatureComponent, ComponentShutdown>(OnLegendaryCreatureShutdown);
        SubscribeNetworkEvent<TribalHuntJoinRequestEvent>(OnJoinRequest);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_stage == TribalHuntStage.Inactive)
            return;

        if (_stage == TribalHuntStage.Gathering && _timing.CurTime >= _gatheringEndsAt)
        {
            BeginActiveHunt();
            return;
        }

        if (_stage == TribalHuntStage.Active)
        {
            if (_activeLegendaryCreature == null || !Exists(_activeLegendaryCreature.Value))
            {
                EndHunt(Loc.GetString("tribal-hunt-popup-failed"));
                return;
            }

            if (_timing.CurTime >= _huntEndsAt)
            {
                EndHunt(Loc.GetString("tribal-hunt-popup-failed"));
                return;
            }

            if (_timing.CurTime >= _lastLocationBroadcast + TimeSpan.FromSeconds(5))
            {
                _lastLocationBroadcast = _timing.CurTime;
                UpdateCreatureLocation();
            }
        }

        if (_timing.CurTime >= _lastUiHeartbeat + TimeSpan.FromSeconds(1))
        {
            _lastUiHeartbeat = _timing.CurTime;
            BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
        }
    }

    private void OnLeaderStartup(EntityUid uid, TribalHuntLeaderComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.StartActionEntity, component.StartAction);
    }

    private void OnLeaderShutdown(EntityUid uid, TribalHuntLeaderComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.StartActionEntity);
    }

    private void OnParticipantStartup(EntityUid uid, TribalHuntParticipantComponent component, ComponentStartup args)
    {
        _actions.AddAction(uid, ref component.OpenTrackerActionEntity, component.OpenTrackerAction);

        if (_stage != TribalHuntStage.Inactive)
            SendUiUpdate(uid, BuildStatusText());
    }

    private void OnParticipantShutdown(EntityUid uid, TribalHuntParticipantComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.OpenTrackerActionEntity);
    }

    private void OnToggleHuntGuiAction(EntityUid uid, TribalHuntParticipantComponent component, PerformTribalToggleHuntGuiActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        RaiseNetworkEvent(new TribalHuntToggleWindowEvent(), actor.PlayerSession);
    }

    private void OnStartHuntAction(EntityUid uid, TribalHuntLeaderComponent component, PerformTribalStartHuntActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanLeadHunt(uid, component))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-cannot-lead"));
            return;
        }

        if (_stage != TribalHuntStage.Inactive)
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-already-active"));
            return;
        }

        _stage = TribalHuntStage.Gathering;
        _targetDepartment = component.TargetDepartment;
        _chief = uid;
        _activeHuntSessionId = uid;
        _joinedHunters.Clear();
        _joinedHunters.Add(uid);
        _gatheringEndsAt = _timing.CurTime + TimeSpan.FromMinutes(2);
        _configuredHuntDuration = component.HuntDuration;
        _configuredRewardDuration = component.RewardDuration;
        _configuredRewardSpeedBonus = component.RewardSpeedBonus;
        _activeLegendaryCreature = null;
        _lastLocationBroadcast = _timing.CurTime;
        _lastUiHeartbeat = TimeSpan.Zero;
        _lastKnownCoordinates = Loc.GetString("tribal-hunt-gui-coordinate-pending");
        _hasKnownCoordinates = false;

        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void OnJoinRequest(TribalHuntJoinRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        if (_stage != TribalHuntStage.Gathering)
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-join-closed"));
            return;
        }

        if (!IsInDepartment(uid, _targetDepartment))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-not-tribe"));
            return;
        }

        if (!_joinedHunters.Add(uid))
        {
            SendUiUpdate(uid, Loc.GetString("tribal-hunt-popup-already-joined"));
            return;
        }

        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void BeginActiveHunt()
    {
        if (_stage != TribalHuntStage.Gathering || _chief == null)
        {
            EndHunt(Loc.GetString("tribal-hunt-popup-failed"));
            return;
        }

        if (!TryComp(_chief.Value, out TransformComponent? chiefXform) || chiefXform.MapID == MapId.Nullspace)
        {
            EndHunt(Loc.GetString("tribal-hunt-popup-failed"));
            return;
        }

        var chiefMapCoords = _transform.GetMapCoordinates(_chief.Value, chiefXform);

        _activeLegendaryCreature = _legendarySpawner.TrySpawnLegendaryCreature(
            "N14MobDeathclaw",
            _activeHuntSessionId ?? _chief.Value,
            chiefMapCoords);

        if (_activeLegendaryCreature == null)
        {
            EndHunt(Loc.GetString("tribal-hunt-popup-failed"));
            return;
        }

        _stage = TribalHuntStage.Active;
        _huntEndsAt = _timing.CurTime + _configuredHuntDuration;
        _lastLocationBroadcast = TimeSpan.Zero;
        UpdateCreatureLocation();
        BroadcastUiToDepartment(_targetDepartment, BuildStatusText());
    }

    private void UpdateCreatureLocation()
    {
        if (_activeLegendaryCreature == null || !Exists(_activeLegendaryCreature.Value))
            return;

        var mapCoords = _transform.GetMapCoordinates(_activeLegendaryCreature.Value);
        var position = mapCoords.Position;
        _lastKnownCoordinates = Loc.GetString("tribal-hunt-gui-coordinate-format",
            ("x", MathF.Round(position.X, 1)),
            ("y", MathF.Round(position.Y, 1)));
        _hasKnownCoordinates = true;
    }

    private void CompleteHunt()
    {
        var expiresAt = _timing.CurTime + _configuredRewardDuration;
        var participants = EntityQueryEnumerator<TribalHuntParticipantComponent>();

        while (participants.MoveNext(out var uid, out _))
        {
            if (!_joinedHunters.Contains(uid))
                continue;

            if (!IsInDepartment(uid, _targetDepartment))
                continue;

            if (_mobState.IsDead(uid) || !HasComp<MovementSpeedModifierComponent>(uid))
                continue;

            var buff = EnsureComp<WarcryBuffComponent>(uid);
            buff.SpeedBonus = Math.Max(buff.SpeedBonus, _configuredRewardSpeedBonus);
            if (expiresAt > buff.ExpiresAt)
                buff.ExpiresAt = expiresAt;

            Dirty(uid, buff);
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
        }

        EndHunt(Loc.GetString("tribal-hunt-popup-complete",
            ("seconds", (int) Math.Ceiling(_configuredRewardDuration.TotalSeconds))), cleanupLegendary: false);
    }

    private void EndHunt(string statusText, bool cleanupLegendary = true)
    {
        var department = _targetDepartment;
        var legendary = _activeLegendaryCreature;

        _stage = TribalHuntStage.Inactive;
        _activeLegendaryCreature = null;
        _activeHuntSessionId = null;
        _chief = null;
        _lastKnownCoordinates = string.Empty;
        _hasKnownCoordinates = false;
        _joinedHunters.Clear();

        if (cleanupLegendary && legendary != null && Exists(legendary.Value))
            Del(legendary.Value);

        BroadcastUiToDepartment(department, statusText);
    }

    private string BuildStatusText()
    {
        return _stage switch
        {
            TribalHuntStage.Gathering => Loc.GetString("tribal-hunt-gui-status-gathering"),
            TribalHuntStage.Active => Loc.GetString("tribal-hunt-gui-status-active"),
            _ => Loc.GetString("tribal-hunt-gui-status-idle"),
        };
    }

    private bool CanLeadHunt(EntityUid uid, TribalHuntLeaderComponent component)
    {
        if (!IsInDepartment(uid, component.TargetDepartment))
            return false;

        if (component.ActivatorJobs == null || component.ActivatorJobs.Count == 0)
            return true;

        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var prototype))
            return false;

        return component.ActivatorJobs.Contains(prototype.ID);
    }

    private bool IsInDepartment(EntityUid uid, string departmentId)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out _, out var jobPrototype))
            return false;

        return _jobs.TryGetDepartment(jobPrototype.ID, out var department) && department.ID == departmentId;
    }

    private void BroadcastUiToDepartment(string departmentId, string statusText)
    {
        var query = EntityQueryEnumerator<TribalHuntParticipantComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!IsInDepartment(uid, departmentId))
                continue;

            SendUiUpdate(uid, statusText);
        }
    }

    private void SendUiUpdate(EntityUid uid, string statusText)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        if (_mobState.IsDead(uid))
            return;

        var remaining = _stage switch
        {
            TribalHuntStage.Gathering => Math.Max(0, (int) Math.Ceiling((_gatheringEndsAt - _timing.CurTime).TotalSeconds)),
            TribalHuntStage.Active => Math.Max(0, (int) Math.Ceiling((_huntEndsAt - _timing.CurTime).TotalSeconds)),
            _ => 0,
        };

        var isJoined = _joinedHunters.Contains(uid);

        var state = new TribalHuntUiState
        {
            Active = _stage == TribalHuntStage.Active,
            Offered = 0,
            Required = 0,
            SecondsRemaining = remaining,
            StatusText = statusText,
            CoordinatesKnown = _stage == TribalHuntStage.Active && _hasKnownCoordinates,
            CoordinatesText = _lastKnownCoordinates,
            JoinWindowOpen = _stage == TribalHuntStage.Gathering,
            CanJoin = _stage == TribalHuntStage.Gathering && !isJoined && IsInDepartment(uid, _targetDepartment),
            IsJoined = isJoined,
            JoinedHunters = _joinedHunters.Count,
        };

        RaiseNetworkEvent(new TribalHuntUiUpdateEvent { State = state }, actor.PlayerSession);
    }

    private void OnLegendaryCreatureKilled(EntityUid uid, LegendaryCreatureComponent component, LegendaryCreatureKilledEvent args)
    {
        if (_stage != TribalHuntStage.Active || _activeLegendaryCreature != uid)
            return;

        CompleteHunt();
    }

    private void OnLegendaryCreatureShutdown(EntityUid uid, LegendaryCreatureComponent component, ComponentShutdown args)
    {
        if (_stage == TribalHuntStage.Active && _activeLegendaryCreature == uid)
            EndHunt(Loc.GetString("tribal-hunt-popup-failed"), cleanupLegendary: false);
    }
}
