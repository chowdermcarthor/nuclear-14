// #Misfits Change - Wasteland Map Viewer
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
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
/// BUI state sent from server to client when the map UI is opened.
/// Uses primitive floats because Box2 is not [NetSerializable].
/// </summary>
[Serializable, NetSerializable]
public sealed class WastelandMapBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string MapTitle;
    public readonly string MapTexturePath;
    // World-space bounds as separate floats (left, bottom, right, top).
    public readonly float BoundsLeft;
    public readonly float BoundsBottom;
    public readonly float BoundsRight;
    public readonly float BoundsTop;

    public WastelandMapBoundUserInterfaceState(string mapTitle, string mapTexturePath,
        float boundsLeft, float boundsBottom, float boundsRight, float boundsTop)
    {
        MapTitle = mapTitle;
        MapTexturePath = mapTexturePath;
        BoundsLeft = boundsLeft;
        BoundsBottom = boundsBottom;
        BoundsRight = boundsRight;
        BoundsTop = boundsTop;
    }
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

    /// <summary>
    /// The world-space tile bounds (left, bottom, right, top) that the map image covers.
    /// Used server-side to populate the BUI state. NOT AutoNetworkedField because Box2
    /// is not [NetSerializable] in RobustToolbox.
    /// </summary>
    [DataField]
    public Box2 WorldBounds = default;
}

