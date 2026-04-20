// #Misfits Add — Spirit Board component. Holds config for the séance mechanic.
// When a TribalShaman activates the board, a SpiritBoardSessionComponent is added.
// Nearby ghosts can then commune with the living via a response-selection BUI.

namespace Content.Shared._Misfits.SpiritBoard;

/// <summary>
/// Core config component for the Spirit Board (Ouija) entity.
/// Allows a TribalShaman to start a séance; nearby ghosts may respond.
/// </summary>
[RegisterComponent]
public sealed partial class SpiritBoardComponent : Component
{
    /// <summary>
    /// How long a séance session lasts before auto-closing.
    /// </summary>
    [DataField]
    public TimeSpan SessionDuration = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Cooldown between séances.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(3);

    /// <summary>
    /// When the current cooldown expires. Null = never used.
    /// </summary>
    [DataField]
    public TimeSpan? CooldownEnd;

    /// <summary>
    /// Department ID whose living members receive ghost responses.
    /// </summary>
    [DataField]
    public string TargetDepartment = "Tribe";

    /// <summary>
    /// Job IDs allowed to start a séance.
    /// </summary>
    [DataField]
    public HashSet<string> ActivatorJobs = new() { "TribalShaman", "TribalElder" };

    /// <summary>
    /// Radius (tiles) within which ghosts can interact with the board.
    /// </summary>
    [DataField]
    public float GhostRange = 6f;

    /// <summary>
    /// Radius (tiles) within which living tribe members receive the response popup.
    /// </summary>
    [DataField]
    public float BroadcastRange = 9f;
}

/// <summary>
/// Marker component added to the board for the duration of an active séance session.
/// Removed when the session ends (timeout, GOODBYE response, or shaman termination).
/// </summary>
[RegisterComponent]
public sealed partial class SpiritBoardSessionComponent : Component
{
    /// <summary>
    /// Entity UID of the shaman who started this séance.
    /// </summary>
    [DataField]
    public EntityUid Shaman;

    /// <summary>
    /// When the session automatically ends.
    /// </summary>
    [DataField]
    public TimeSpan SessionEnd;
}
