// #Misfits Add - Vaccinator machine component.
// Accepts a diagnosis report and produces a vaccine for the identified disease.

using Content.Shared._Misfits.Disease;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Machine that reads a disease diagnosis from a paper report
/// and produces a vaccine syringe immunizing against that disease.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseVaccinatorComponent : Component
{
    /// <summary>Seconds to create a vaccine.</summary>
    [DataField]
    public float ProcessDelay = 8f;

    /// <summary>Whether the machine is currently processing.</summary>
    [ViewVariables]
    public bool Running;

    /// <summary>Accumulated processing time.</summary>
    [ViewVariables]
    public float Accumulator;

    /// <summary>The disease identified from the inserted report.</summary>
    [ViewVariables]
    public ProtoId<DiseasePrototype>? QueuedDisease;
}
