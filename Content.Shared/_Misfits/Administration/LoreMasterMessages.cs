// #Misfits Change /Add/ - Shared network messages for the LoreMaster admin tab.
// Allows admins to inspect faction objective states and issue new objectives to faction members.
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Administration;

/// <summary>
/// Client → server: request current objective state for all online members of a faction.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestLoreMasterFactionInfoEvent : EntityEventArgs
{
    public string FactionId = string.Empty;
}

/// <summary>
/// Server → client: current objective snapshot for all online members of a faction, sorted highest rank first.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoreMasterFactionInfoEvent : EntityEventArgs
{
    public string FactionId = string.Empty;
    /// <summary>Members ordered highest → lowest job weight (most senior first).</summary>
    public List<LoreMasterMemberInfo> Members = new();
}

/// <summary>
/// Snapshot of a single online faction member for the Loremaster UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoreMasterMemberInfo
{
    public string PlayerName = string.Empty;
    public string JobName = string.Empty;
    /// <summary>Job prototype weight — higher means more senior.</summary>
    public int JobWeight;
    public List<LoreMasterObjectiveSnapshot> Objectives = new();
}

/// <summary>
/// Minimal objective data transmitted to the admin client — title, description, progress percentage.
/// Icon is omitted because SpriteSpecifier is not trivially serializable over the network here.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoreMasterObjectiveSnapshot
{
    public string Title = string.Empty;
    public string Description = string.Empty;
    /// <summary>0.0 – 1.0 completion fraction.</summary>
    public float Progress;
}

/// <summary>
/// Admin → server: issue a specific objective prototype to a chosen online member of a faction.
/// </summary>
[Serializable, NetSerializable]
public sealed class IssueLoreMasterObjectiveEvent : EntityEventArgs
{
    public string FactionId = string.Empty;
    /// <summary>Entity prototype ID of the objective to create, e.g. "KillNCRHeadObjective".</summary>
    public string ObjectivePrototype = string.Empty;
    /// <summary>Player name of the target faction member. Falls back to highest-ranked if empty.</summary>
    // #Misfits Tweak - added so admins can target a specific member rather than always the top-ranked.
    public string TargetPlayerName = string.Empty;
}

/// <summary>
/// Admin → server: issue a fully custom (admin-typed) objective to a chosen online member of a faction.
/// Title and description are provided directly; no prototype is used.
/// </summary>
[Serializable, NetSerializable]
public sealed class IssueCustomLoreMasterObjectiveEvent : EntityEventArgs
{
    public string FactionId = string.Empty;
    /// <summary>Player name of the target faction member. Falls back to highest-ranked if empty.</summary>
    // #Misfits Tweak - added so admins can target a specific member rather than always the top-ranked.
    public string TargetPlayerName = string.Empty;
    /// <summary>Admin-supplied title shown in the C menu objective list.</summary>
    public string CustomTitle = string.Empty;
    /// <summary>Admin-supplied description shown beneath the title.</summary>
    public string CustomDescription = string.Empty;
}

/// <summary>
/// Server → admin client: result of an objective-issuance attempt.
/// </summary>
[Serializable, NetSerializable]
public sealed class LoreMasterObjectiveResultEvent : EntityEventArgs
{
    public bool Success;
    public string Message = string.Empty;
}

/// <summary>
/// Admin → server: remove a specific objective from a faction member by matching its title.
/// </summary>
// #Misfits Add - allows admins to revoke issued orders from the Loremaster tab.
[Serializable, NetSerializable]
public sealed class RemoveLoreMasterObjectiveEvent : EntityEventArgs
{
    public string FactionId = string.Empty;
    /// <summary>Player name of the faction member whose objective should be removed.</summary>
    public string TargetPlayerName = string.Empty;
    /// <summary>Exact title of the objective to remove (matched against Name(objectiveEntity)).</summary>
    public string ObjectiveTitle = string.Empty;
}
