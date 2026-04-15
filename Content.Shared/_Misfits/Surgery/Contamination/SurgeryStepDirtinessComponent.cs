// #Misfits Change - Ported from Delta-V surgery contamination system
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Surgery.Contamination;

/// <summary>
///     Component that allows a surgery step to increase tools and gloves' dirtiness.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class SurgeryStepDirtinessComponent : Component
{
    /// <summary>
    ///     The amount of dirtiness this step should add to tools on completion.
    /// </summary>
    [DataField]
    public FixedPoint2 ToolDirtiness = 0.5;

    /// <summary>
    ///     The amount of dirtiness this step should add to gloves on completion.
    /// </summary>
    [DataField]
    public FixedPoint2 GloveDirtiness = 0.5;
}
