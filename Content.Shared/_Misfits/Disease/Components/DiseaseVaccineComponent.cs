// #Misfits Add - Disease vaccine component.
// Prevents a specific disease when used on an entity. Single-use syringe.

using Content.Shared._Misfits.Disease;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Single-use vaccine for a specific disease. When injected, adds the disease
/// to the target's PastDiseases (immunity) without causing infection.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseVaccineComponent : Component
{
    /// <summary>The disease this vaccine immunizes against.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<DiseasePrototype>? Disease;

    /// <summary>Whether this vaccine has already been used.</summary>
    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>Seconds to inject the vaccine.</summary>
    [DataField]
    public float InjectDelay = 3f;
}

/// <summary>DoAfter event for injecting a vaccine.</summary>
[Serializable, NetSerializable]
public sealed partial class DiseaseVaccineDoAfterEvent : SimpleDoAfterEvent;
