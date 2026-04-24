using System;
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
    public EntProtoId<InstantActionComponent> StartMinorHuntAction = "ActionTribalStartMinorHunt";

    [DataField]
    public EntityUid? StartMinorHuntActionEntity;

    [DataField]
    public string TargetDepartment = "Tribe";

    [DataField]
    public TimeSpan MinorHuntDuration = TimeSpan.FromMinutes(8);

    [DataField]
    public TimeSpan MinorRewardDuration = TimeSpan.FromMinutes(2);

    [DataField]
    public float MinorRewardSpeedBonus = 0.10f;
}
