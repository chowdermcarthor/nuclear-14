using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.TribalHunt;

/// <summary>
/// Grants the elder action that starts a timed tribal hunt contract.
/// </summary>
[RegisterComponent]
public sealed partial class TribalHuntLeaderComponent : Component
{
    [DataField]
    public EntProtoId<InstantActionComponent> StartAction = "ActionTribalStartHunt";

    [DataField]
    public EntityUid? StartActionEntity;

    /// <summary>
    /// Department used for participant validation.
    /// </summary>
    [DataField]
    public string TargetDepartment = "Tribe";

    [DataField]
    public int RequiredOfferings = 8;

    [DataField]
    public TimeSpan HuntDuration = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan RewardDuration = TimeSpan.FromMinutes(3);

    [DataField]
    public float RewardSpeedBonus = 0.20f;

    [DataField]
    public HashSet<string>? ActivatorJobs;
}
