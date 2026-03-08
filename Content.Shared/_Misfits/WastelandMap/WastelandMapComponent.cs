// #Misfits Change - Wasteland Map Viewer
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.WastelandMap;

/// <summary>
/// UI key for the wasteland map viewer interface.
/// </summary>
[Serializable, NetSerializable]
public enum WastelandMapUiKey : byte
{
    Key,
}

/// <summary>
/// Component that marks an entity as a wasteland map viewer.
/// Displays a static map image when used.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WastelandMapComponent : Component
{
    /// <summary>
    /// Path to the map texture to display, relative to Resources/Textures/.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public ResPath MapTexturePath = default!;

    /// <summary>
    /// Title displayed on the map window.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string MapTitle = "Map";
}
