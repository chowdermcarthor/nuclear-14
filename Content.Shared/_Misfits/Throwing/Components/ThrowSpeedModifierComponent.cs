// #Misfits Change - Per-item throw speed tuning for wasteland throwables
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Throwing.Components;

/// <summary>
/// Scales throw speed for a held item when it is thrown normally.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class ThrowSpeedModifierComponent : Component
{
    /// <summary>
    /// Multiplier applied to the thrower's base throw speed.
    /// </summary>
    [DataField(required: true)]
    public float Multiplier = 1f;
}