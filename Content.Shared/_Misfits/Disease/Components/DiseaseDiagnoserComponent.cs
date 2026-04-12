// #Misfits Add - Disease diagnoser machine component.
// Accepts a used disease swab, processes it, and produces a diagnosis report.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Machine that accepts a used DiseaseSwab and produces a paper report
/// identifying the disease. Output can then be fed to the Vaccinator.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseDiagnoserComponent : Component
{
    /// <summary>Seconds to process a swab sample.</summary>
    [DataField]
    public float ProcessDelay = 5f;

    /// <summary>Whether the machine is currently processing.</summary>
    [ViewVariables]
    public bool Running;

    /// <summary>Accumulated processing time.</summary>
    [ViewVariables]
    public float Accumulator;

    /// <summary>Disease queued for processing from inserted swab.</summary>
    [ViewVariables]
    public ProtoId<DiseasePrototype>? QueuedDisease;
}
