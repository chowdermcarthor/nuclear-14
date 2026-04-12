// #Misfits Add - Disease diagnosis tag component.
// Attached to DiagnosisReportPaper entities to carry the disease prototype ID
// for use by the vaccinator machine without parsing paper text.

using Content.Shared._Misfits.Disease;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Marker component added to diagnosis papers to carry the disease ID.
/// Used by the vaccinator to know which vaccine to produce.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseDiagnosisComponent : Component
{
    /// <summary>Disease prototype this diagnosis refers to.</summary>
    [DataField]
    public ProtoId<DiseasePrototype>? Disease;
}
