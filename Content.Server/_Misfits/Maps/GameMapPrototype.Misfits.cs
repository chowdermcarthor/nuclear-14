// #Misfits Add - Map author/credit field for lobby display
using Robust.Shared.Prototypes;

namespace Content.Server.Maps;

/// <summary>
/// Misfits extension: adds an optional author credit to the game map prototype
/// so it can be displayed in the lobby UI.
/// </summary>
public sealed partial class GameMapPrototype
{
    /// <summary>
    /// The author/creator of this map, displayed in the lobby.
    /// </summary>
    [DataField("mapAuthor")]
    public string? MapAuthor { get; private set; }
}
