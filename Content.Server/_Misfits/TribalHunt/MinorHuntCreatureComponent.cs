using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Marks creatures spawned as part of a tribal minor hunt pack.
/// </summary>
[RegisterComponent]
public sealed partial class MinorHuntCreatureComponent : Component
{
    /// <summary>
    /// Hunt-session identity, currently the initiating hunter UID.
    /// </summary>
    [DataField]
    public EntityUid? HuntSessionId;

    /// <summary>
    /// Creature name used for hunt tracker text and map labels.
    /// </summary>
    [DataField]
    public string CreatureName = "prey";

    /// <summary>
    /// Whether this target should appear on tribe tactical feeds.
    /// </summary>
    [DataField]
    public bool RevealLocation = true;
}
