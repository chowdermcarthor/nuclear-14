// #Misfits Add - Disease swab component.
// A single-use swab that can collect a disease sample from a sick entity.

using Content.Shared._Misfits.Disease;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Single-use swab for collecting a disease sample from a DiseaseCarrier.
/// Used with the DiseaseDiagnoser machine for diagnosis.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseSwabComponent : Component
{
    /// <summary>Whether the swab has been used to collect a sample.</summary>
    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>The disease collected on this swab (null if unused).</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<DiseasePrototype>? Disease;

    /// <summary>How long it takes to swab a patient in seconds.</summary>
    [DataField]
    public float SwabDelay = 2f;
}

/// <summary>DoAfter event for swabbing a sick entity.</summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseSwabDoAfterEvent : SimpleDoAfterEvent;
