// #Misfits Change /Add:/ Prevent self-unbuckling from selected strap entities.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Buckle;

[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class NoSelfUnbuckleComponent : Component
{
    [DataField]
    public LocId Popup = "buckle-component-no-self-unbuckle-message";
}