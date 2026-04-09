using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.TribalHunt;

/// <summary>
/// Marks entities that can receive and interact with tribal hunt GUI updates.
/// </summary>
[RegisterComponent]
public sealed partial class TribalHuntParticipantComponent : Component
{
    [DataField]
    public EntProtoId<InstantActionComponent> OpenTrackerAction = "ActionTribalToggleHuntGui";

    [DataField]
    public EntityUid? OpenTrackerActionEntity;

    [DataField]
    public string TargetDepartment = "Tribe";
}
