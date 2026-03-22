using Robust.Shared.Map;

namespace Content.Server._N14.PortalAutoLink;

/// <summary>
/// Enables the automatic linking of entities by matching keys during searches.
/// </summary>
[RegisterComponent, Access(typeof(PortalAutoLinkSystem))]
public sealed partial class PortalAutoLinkComponent : Component
{
    /// <summary>
    /// A key used to locate another entity with a matching link in the world.
    /// Null or empty means "do not auto-link" — the previous default of "IgnoreMe"
    /// was a footgun because all keyless stairs would link to each other.
    /// </summary>
    [DataField]
    public string? LinkKey { get; set; } = null;
}
