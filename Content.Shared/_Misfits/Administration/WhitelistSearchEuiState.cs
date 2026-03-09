// #Misfits Change - Shared state and messages for the Whitelist Search EUI
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration;

/// <summary>
/// State sent from server to client for the whitelist search EUI.
/// Contains search results and the currently selected player's whitelists.
/// </summary>
[Serializable, NetSerializable]
public sealed class WhitelistSearchEuiState : EuiStateBase
{
    public bool CanManagePlaytime;
    public bool CanManageSlots;

    /// <summary>
    /// Player search results matching the last query.
    /// </summary>
    public List<WhitelistPlayerInfo> SearchResults;

    /// <summary>
    /// The currently selected player's name, or null if none selected.
    /// </summary>
    public string? SelectedPlayerName;

    /// <summary>
    /// The currently selected player's user ID, or null if none selected.
    /// </summary>
    public NetUserId? SelectedPlayerId;

    /// <summary>
    /// The currently selected player's job whitelists, or null if none selected.
    /// </summary>
    public HashSet<ProtoId<JobPrototype>>? Whitelists;

    /// <summary>
    /// Extra per-job admin data for the selected player.
    /// </summary>
    public List<WhitelistJobAdminInfo>? JobAdminInfo;

    /// <summary>
    /// The station whose slots are being managed, or null if no station could be resolved.
    /// </summary>
    public string? SelectedStationName;

    public WhitelistSearchEuiState(
        bool canManagePlaytime,
        bool canManageSlots,
        List<WhitelistPlayerInfo> searchResults,
        string? selectedPlayerName,
        NetUserId? selectedPlayerId,
        HashSet<ProtoId<JobPrototype>>? whitelists,
        List<WhitelistJobAdminInfo>? jobAdminInfo,
        string? selectedStationName)
    {
        CanManagePlaytime = canManagePlaytime;
        CanManageSlots = canManageSlots;
        SearchResults = searchResults;
        SelectedPlayerName = selectedPlayerName;
        SelectedPlayerId = selectedPlayerId;
        Whitelists = whitelists;
        JobAdminInfo = jobAdminInfo;
        SelectedStationName = selectedStationName;
    }
}

[Serializable, NetSerializable]
public sealed class WhitelistJobAdminInfo
{
    public ProtoId<JobPrototype> Job;
    public TimeSpan RoleTime;
    public int? Slots;
    public bool HasSlotConfiguration;

    public WhitelistJobAdminInfo(
        ProtoId<JobPrototype> job,
        TimeSpan roleTime,
        int? slots,
        bool hasSlotConfiguration)
    {
        Job = job;
        RoleTime = roleTime;
        Slots = slots;
        HasSlotConfiguration = hasSlotConfiguration;
    }
}

/// <summary>
/// Lightweight player info for search results.
/// </summary>
[Serializable, NetSerializable]
public sealed class WhitelistPlayerInfo
{
    public NetUserId UserId;
    public string UserName;

    public WhitelistPlayerInfo(NetUserId userId, string userName)
    {
        UserId = userId;
        UserName = userName;
    }
}

/// <summary>
/// Message from client to server to search for players by partial name.
/// </summary>
[Serializable, NetSerializable]
public sealed class SearchPlayersMessage : EuiMessageBase
{
    public string Query;

    public SearchPlayersMessage(string query)
    {
        Query = query;
    }
}

/// <summary>
/// Message from client to server to select a player and load their whitelists.
/// </summary>
[Serializable, NetSerializable]
public sealed class SelectPlayerMessage : EuiMessageBase
{
    public NetUserId PlayerId;

    public SelectPlayerMessage(NetUserId playerId)
    {
        PlayerId = playerId;
    }
}

/// <summary>
/// Message from client to server to set a job whitelist for the selected player.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetWhitelistSearchJobMessage : EuiMessageBase
{
    public ProtoId<JobPrototype> Job;
    public bool Whitelisting;

    public SetWhitelistSearchJobMessage(ProtoId<JobPrototype> job, bool whitelisting)
    {
        Job = job;
        Whitelisting = whitelisting;
    }
}

/// <summary>
/// Message from client to server to add role playtime for the selected player.
/// </summary>
[Serializable, NetSerializable]
public sealed class AddWhitelistSearchRoleTimeMessage : EuiMessageBase
{
    public ProtoId<JobPrototype> Job;
    public string TimeString;

    public AddWhitelistSearchRoleTimeMessage(ProtoId<JobPrototype> job, string timeString)
    {
        Job = job;
        TimeString = timeString;
    }
}

/// <summary>
/// Message from client to server to adjust job slots on the selected station.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdjustWhitelistSearchJobSlotsMessage : EuiMessageBase
{
    public ProtoId<JobPrototype> Job;
    public int Delta;

    public AdjustWhitelistSearchJobSlotsMessage(ProtoId<JobPrototype> job, int delta)
    {
        Job = job;
        Delta = delta;
    }
}

/// <summary>
/// Message from client to server to set (overwrite) a single job's role time to an exact value.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetWhitelistSearchRoleTimeMessage : EuiMessageBase
{
    public ProtoId<JobPrototype> Job;
    public string TimeString;

    public SetWhitelistSearchRoleTimeMessage(ProtoId<JobPrototype> job, string timeString)
    {
        Job = job;
        TimeString = timeString;
    }
}

/// <summary>
/// Message from client to server to add playtime to all jobs in a department for the selected player.
/// </summary>
[Serializable, NetSerializable]
public sealed class AddWhitelistSearchDeptTimeMessage : EuiMessageBase
{
    public string DepartmentId;
    public string TimeString;

    public AddWhitelistSearchDeptTimeMessage(string departmentId, string timeString)
    {
        DepartmentId = departmentId;
        TimeString = timeString;
    }
}

/// <summary>
/// Message from client to server to set (overwrite) playtime for all jobs in a department.
/// </summary>
[Serializable, NetSerializable]
public sealed class SetWhitelistSearchDeptTimeMessage : EuiMessageBase
{
    public string DepartmentId;
    public string TimeString;

    public SetWhitelistSearchDeptTimeMessage(string departmentId, string timeString)
    {
        DepartmentId = departmentId;
        TimeString = timeString;
    }
}
