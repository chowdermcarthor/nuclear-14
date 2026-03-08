// #Misfits Change - Server-side EUI for the Whitelist Search admin panel
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.Players.JobWhitelist;
using Content.Shared._Misfits.Administration;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.Roles;
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
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;

    private readonly ISawmill _sawmill;

    private List<WhitelistPlayerInfo> _searchResults = new();
    private NetUserId? _selectedPlayerId;
    private string? _selectedPlayerName;
    private HashSet<ProtoId<JobPrototype>>? _whitelists;

    public WhitelistSearchEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _log.GetSawmill("admin.whitelist_search_eui");
    }

    public override EuiStateBase GetNewState()
    {
        return new WhitelistSearchEuiState(
            _searchResults,
            _selectedPlayerName,
            _selectedPlayerId,
            _whitelists);
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Whitelist))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to use whitelist search without permission");
            return;
        }

        switch (msg)
        {
            case SearchPlayersMessage search:
                await HandleSearch(search.Query);
                break;
            case SelectPlayerMessage select:
                await HandleSelectPlayer(select.PlayerId);
                break;
            case SetWhitelistSearchJobMessage setJob:
                HandleSetJob(setJob.Job, setJob.Whitelisting);
                break;
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
}
