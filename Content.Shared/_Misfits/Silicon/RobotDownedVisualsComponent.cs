// #Misfits Change - Lets player robots swap to a non-hovering sprite while downed but still alive.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Silicon;

/// <summary>
/// Configures the base sprite states to use while a robot is standing versus lying down.
/// This is client-driven off networked standing and mob state data.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class RobotDownedVisualsComponent : Component
{
    [DataField]
    public string StandingState = "icon";

    [DataField]
    public string DownedState = "dead";
}