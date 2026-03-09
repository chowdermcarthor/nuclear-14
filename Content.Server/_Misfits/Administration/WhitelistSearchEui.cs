// #Misfits Change - Server-side EUI for the Whitelist Search admin panel
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Misfits.Administration;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Administration;

public sealed class WhitelistSearchEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private readonly ISawmill _sawmill;
    private StationJobsSystem StationJobs => _entManager.System<StationJobsSystem>();
    private StationSystem StationSystem => _entManager.System<StationSystem>();

    private static readonly HashSet<string> AllowedDepartments = new()
    {
        "BrotherhoodOfSteel",
        "CaesarLegion",
        "NCR",
        "Townsfolk",
        "FEVMutants",
        "Tribe",
        "Robots",
        "Vault",
        "Raider",
    };

    private List<WhitelistPlayerInfo> _searchResults = new();
    private NetUserId? _selectedPlayerId;
    private string? _selectedPlayerName;
    private HashSet<ProtoId<JobPrototype>>? _whitelists;
    private List<WhitelistJobAdminInfo>? _jobAdminInfo;
    private EntityUid? _selectedStation;
    private string? _selectedStationName;

    public WhitelistSearchEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("admin.whitelist_search_eui");
    }

    public override EuiStateBase GetNewState()
    {
        return new WhitelistSearchEuiState(
            _admin.HasAdminFlag(Player, AdminFlags.Admin),
            _admin.HasAdminFlag(Player, AdminFlags.Admin),
            _searchResults,
            _selectedPlayerName,
            _selectedPlayerId,
            _whitelists,
            _jobAdminInfo,
            _selectedStationName);
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        _sawmill.Debug($"Received EUI message of type: {msg.GetType().Name}");

        try
        {
            if (!_admin.HasAdminFlag(Player, AdminFlags.Whitelist))
            {
                _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to use whitelist search without permission");
                return;
            }

            switch (msg)
            {
                case SearchPlayersMessage search:
                    _sawmill.Debug($"Handling search for: '{search.Query}'");
                    await HandleSearch(search.Query);
                    break;
                case SelectPlayerMessage select:
                    await HandleSelectPlayer(select.PlayerId);
                    break;
                case SetWhitelistSearchJobMessage setJob:
                    HandleSetJob(setJob.Job, setJob.Whitelisting);
                    break;
                case AddWhitelistSearchRoleTimeMessage addTime:
                    await HandleAddRoleTime(addTime.Job, addTime.TimeString);
                    break;
                case SetWhitelistSearchRoleTimeMessage setTime:
                    await HandleSetRoleTime(setTime.Job, setTime.TimeString);
                    break;
                case AddWhitelistSearchDeptTimeMessage addDept:
                    await HandleAddDeptTime(addDept.DepartmentId, addDept.TimeString);
                    break;
                case SetWhitelistSearchDeptTimeMessage setDept:
                    await HandleSetDeptTime(setDept.DepartmentId, setDept.TimeString);
                    break;
                case AdjustWhitelistSearchJobSlotsMessage adjustSlots:
                    HandleAdjustJobSlots(adjustSlots.Job, adjustSlots.Delta);
                    break;
                default:
                    _sawmill.Debug($"Unknown message type: {msg.GetType().FullName}");
                    break;
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error handling EUI message: {e}");
        }
    }

    private async Task HandleSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            _searchResults = new List<WhitelistPlayerInfo>();
            StateDirty();
            return;
        }

        try
        {
            var records = await _db.SearchPlayersByName(query);
            _sawmill.Debug($"Search for '{query}' returned {records.Count} results");
            _searchResults = records
                .Select(r => new WhitelistPlayerInfo(r.UserId, r.LastSeenUserName))
                .ToList();
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error searching players by name '{query}': {e}");
            _searchResults = new List<WhitelistPlayerInfo>();
        }

        StateDirty();
    }

    private async Task HandleSelectPlayer(NetUserId playerId)
    {
        var record = await _db.GetPlayerRecordByUserId(playerId);
        if (record == null)
        {
            _sawmill.Warning($"Admin {Player.Name} tried to select non-existent player {playerId}");
            return;
        }

        _selectedPlayerId = playerId;
        _selectedPlayerName = record.LastSeenUserName;

        _whitelists = new HashSet<ProtoId<JobPrototype>>();
        var jobs = await _db.GetJobWhitelists(playerId.UserId);
        foreach (var id in jobs)
        {
            if (_proto.HasIndex<JobPrototype>(id))
                _whitelists.Add(id);
        }

        _selectedStation = ResolveTargetStation();
        _selectedStationName = _selectedStation is { } resolvedStation
            && _entManager.TryGetComponent<MetaDataComponent>(resolvedStation, out var meta)
            ? meta.EntityName
            : null;

        _jobAdminInfo = await BuildJobAdminInfo(playerId);

        StateDirty();
    }

    private void HandleSetJob(ProtoId<JobPrototype> job, bool whitelisting)
    {
        if (_selectedPlayerId == null || _whitelists == null)
            return;

        if (!_proto.HasIndex<JobPrototype>(job))
            return;

        if (whitelisting)
        {
            _jobWhitelist.AddWhitelist(_selectedPlayerId.Value, job);
            _whitelists.Add(job);
        }
        else
        {
            _jobWhitelist.RemoveWhitelist(_selectedPlayerId.Value, job);
            _whitelists.Remove(job);
        }

        var verb = whitelisting ? "added" : "removed";
        _sawmill.Info($"{Player.Name} ({Player.UserId}) {verb} whitelist for {job} to player {_selectedPlayerName} ({_selectedPlayerId.Value.UserId})");

        StateDirty();
    }

    private async Task HandleAddRoleTime(ProtoId<JobPrototype> job, string timeString)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to add role time without permission");
            return;
        }

        if (_selectedPlayerId == null || !_proto.TryIndex(job, out var jobProto))
            return;

        var minutes = Content.Server.Administration.Commands.PlayTimeCommandUtilities.CountMinutes(timeString);
        if (minutes <= 0)
            return;

        var delta = TimeSpan.FromMinutes(minutes);
        TimeSpan updatedTime;

        if (_player.TryGetSessionById(_selectedPlayerId.Value, out var session))
        {
            _playTime.FlushTracker(session);
            _playTime.AddTimeToTracker(session, jobProto.PlayTimeTracker, delta);
            _playTime.SaveSession(session);
            updatedTime = _playTime.GetPlayTimeForTracker(session, jobProto.PlayTimeTracker);
        }
        else
        {
            var playTimes = await _db.GetPlayTimes(_selectedPlayerId.Value.UserId);
            var currentTime = playTimes
                .Where(x => x.Tracker == jobProto.PlayTimeTracker)
                .Select(x => x.TimeSpent)
                .FirstOrDefault();

            updatedTime = currentTime + delta;
            await _db.UpdatePlayTimes(new[]
            {
                new PlayTimeUpdate(_selectedPlayerId.Value, jobProto.PlayTimeTracker, updatedTime),
            });
        }

        if (_jobAdminInfo != null)
        {
            var row = _jobAdminInfo.FirstOrDefault(x => x.Job == job);
            if (row != null)
                row.RoleTime = updatedTime;
        }

        _sawmill.Info($"{Player.Name} ({Player.UserId}) added {delta} of role time for {job} to player {_selectedPlayerName} ({_selectedPlayerId.Value.UserId})");
        StateDirty();
    }

    private async Task HandleSetRoleTime(ProtoId<JobPrototype> job, string timeString)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to set role time without permission");
            return;
        }

        if (_selectedPlayerId == null || !_proto.TryIndex(job, out var jobProto))
            return;

        var minutes = Content.Server.Administration.Commands.PlayTimeCommandUtilities.CountMinutes(timeString);
        if (minutes < 0)
            return;

        var newTime = TimeSpan.FromMinutes(minutes);

        if (_player.TryGetSessionById(_selectedPlayerId.Value, out var session))
        {
            _playTime.FlushTracker(session);
            var currentTime = _playTime.GetPlayTimeForTracker(session, jobProto.PlayTimeTracker);
            _playTime.AddTimeToTracker(session, jobProto.PlayTimeTracker, newTime - currentTime);
            _playTime.SaveSession(session);
        }
        else
        {
            await _db.UpdatePlayTimes(new[]
            {
                new PlayTimeUpdate(_selectedPlayerId.Value, jobProto.PlayTimeTracker, newTime),
            });
        }

        if (_jobAdminInfo != null)
        {
            var row = _jobAdminInfo.FirstOrDefault(x => x.Job == job);
            if (row != null)
                row.RoleTime = newTime;
        }

        _sawmill.Info($"{Player.Name} ({Player.UserId}) set role time for {job} to {newTime} for player {_selectedPlayerName} ({_selectedPlayerId.Value.UserId})");
        StateDirty();
    }

    private async Task HandleAddDeptTime(string departmentId, string timeString)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to add department time without permission");
            return;
        }

        if (_selectedPlayerId == null || !_proto.TryIndex<DepartmentPrototype>(departmentId, out var dept))
            return;

        var minutes = Content.Server.Administration.Commands.PlayTimeCommandUtilities.CountMinutes(timeString);
        if (minutes <= 0)
            return;

        var delta = TimeSpan.FromMinutes(minutes);

        if (_player.TryGetSessionById(_selectedPlayerId.Value, out var onlineSession))
        {
            _playTime.FlushTracker(onlineSession);
            foreach (var jobId in dept.Roles)
            {
                if (!_proto.TryIndex(jobId, out var jobProto))
                    continue;
                _playTime.AddTimeToTracker(onlineSession, jobProto.PlayTimeTracker, delta);
            }
            _playTime.SaveSession(onlineSession);

            foreach (var jobId in dept.Roles)
            {
                if (!_proto.TryIndex(jobId, out var jobProto2))
                    continue;
                var updated = _playTime.GetPlayTimeForTracker(onlineSession, jobProto2.PlayTimeTracker);
                var row = _jobAdminInfo?.FirstOrDefault(x => x.Job == jobId);
                if (row != null)
                    row.RoleTime = updated;
            }
        }
        else
        {
            var playTimes = await _db.GetPlayTimes(_selectedPlayerId.Value.UserId);
            var playTimeDict = playTimes.ToDictionary(x => x.Tracker, x => x.TimeSpent);

            var updates = new List<PlayTimeUpdate>();
            foreach (var jobId in dept.Roles)
            {
                if (!_proto.TryIndex(jobId, out var jobProto))
                    continue;
                var current = playTimeDict.GetValueOrDefault(jobProto.PlayTimeTracker);
                var newTime = current + delta;
                updates.Add(new PlayTimeUpdate(_selectedPlayerId.Value, jobProto.PlayTimeTracker, newTime));

                var row = _jobAdminInfo?.FirstOrDefault(x => x.Job == jobId);
                if (row != null)
                    row.RoleTime = newTime;
            }
            await _db.UpdatePlayTimes(updates);
        }

        _sawmill.Info($"{Player.Name} ({Player.UserId}) added {delta} of role time to all jobs in {departmentId} for player {_selectedPlayerName} ({_selectedPlayerId.Value.UserId})");
        StateDirty();
    }

    private async Task HandleSetDeptTime(string departmentId, string timeString)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to set department time without permission");
            return;
        }

        if (_selectedPlayerId == null || !_proto.TryIndex<DepartmentPrototype>(departmentId, out var dept))
            return;

        var minutes = Content.Server.Administration.Commands.PlayTimeCommandUtilities.CountMinutes(timeString);
        if (minutes < 0)
            return;

        var newTime = TimeSpan.FromMinutes(minutes);

        if (_player.TryGetSessionById(_selectedPlayerId.Value, out var onlineSession))
        {
            _playTime.FlushTracker(onlineSession);
            foreach (var jobId in dept.Roles)
            {
                if (!_proto.TryIndex(jobId, out var jobProto))
                    continue;
                var current = _playTime.GetPlayTimeForTracker(onlineSession, jobProto.PlayTimeTracker);
                _playTime.AddTimeToTracker(onlineSession, jobProto.PlayTimeTracker, newTime - current);
            }
            _playTime.SaveSession(onlineSession);
        }
        else
        {
            var updates = new List<PlayTimeUpdate>();
            foreach (var jobId in dept.Roles)
            {
                if (!_proto.TryIndex(jobId, out var jobProto))
                    continue;
                updates.Add(new PlayTimeUpdate(_selectedPlayerId.Value, jobProto.PlayTimeTracker, newTime));
            }
            await _db.UpdatePlayTimes(updates);
        }

        foreach (var jobId in dept.Roles)
        {
            var row = _jobAdminInfo?.FirstOrDefault(x => x.Job == jobId);
            if (row != null)
                row.RoleTime = newTime;
        }

        _sawmill.Info($"{Player.Name} ({Player.UserId}) set role time to {newTime} for all jobs in {departmentId} for player {_selectedPlayerName} ({_selectedPlayerId.Value.UserId})");
        StateDirty();
    }

    private void HandleAdjustJobSlots(ProtoId<JobPrototype> job, int delta)
    {
        if (!_admin.HasAdminFlag(Player, AdminFlags.Admin))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to adjust job slots without permission");
            return;
        }

        if (_selectedStation == null || !_proto.HasIndex<JobPrototype>(job) || delta == 0)
            return;

        if (!StationJobs.TryAdjustJobSlot(_selectedStation.Value, job, delta, createSlot: true, clamp: true))
            return;

        if (_jobAdminInfo != null)
        {
            var row = _jobAdminInfo.FirstOrDefault(x => x.Job == job);
            if (row != null)
            {
                if (StationJobs.TryGetJobSlot(_selectedStation.Value, job, out var slots))
                {
                    row.HasSlotConfiguration = true;
                    row.Slots = slots is null ? null : (int) slots.Value;
                }
                else
                {
                    row.HasSlotConfiguration = false;
                    row.Slots = 0;
                }
            }
        }

        _sawmill.Info($"{Player.Name} ({Player.UserId}) adjusted slots for {job} by {delta} on station {_selectedStationName ?? _selectedStation.Value.ToString()}");
        StateDirty();
    }

    private async Task<List<WhitelistJobAdminInfo>> BuildJobAdminInfo(NetUserId playerId)
    {
        var playTimeDict = new Dictionary<string, TimeSpan>();
        var playTimes = await _db.GetPlayTimes(playerId.UserId);

        foreach (var playTime in playTimes)
        {
            playTimeDict[playTime.Tracker] = playTime.TimeSpent;
        }

        var info = new List<WhitelistJobAdminInfo>();

        foreach (var department in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!AllowedDepartments.Contains(department.ID))
                continue;

            foreach (var jobId in department.Roles)
            {
                if (!_proto.TryIndex(jobId, out var job))
                    continue;

                var roleTime = playTimeDict.GetValueOrDefault(job.PlayTimeTracker);
                var hasSlotConfiguration = false;
                int? slots = 0;

                if (_selectedStation != null && StationJobs.TryGetJobSlot(_selectedStation.Value, jobId, out var slotData))
                {
                    hasSlotConfiguration = true;
                    slots = slotData is null ? null : (int) slotData.Value;
                }

                info.Add(new WhitelistJobAdminInfo(jobId, roleTime, slots, hasSlotConfiguration));
            }
        }

        return info;
    }

    private EntityUid? ResolveTargetStation()
    {
        if (Player.AttachedEntity is { Valid: true } attached &&
            StationSystem.GetOwningStation(attached) is { } currentStation &&
            _entManager.HasComponent<StationJobsComponent>(currentStation))
        {
            return currentStation;
        }

        foreach (var station in StationSystem.GetStations())
        {
            if (_entManager.HasComponent<StationJobsComponent>(station))
                return station;
        }

        return null;
    }
}
