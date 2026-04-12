// #Misfits Add - Disease cure: passage of time.
// Cures the disease after a configurable duration has elapsed.

using Content.Shared._Misfits.Disease;
using Content.Shared._Misfits.Disease.Components;

namespace Content.Server._Misfits.Disease.Cures;

/// <summary>
/// Disease is cured automatically after enough total accumulated disease time.
/// Represents the body naturally fighting off the illness over time.
/// </summary>

public sealed partial class DiseaseJustWaitCure : DiseaseCure
{
    /// <summary>Total seconds of disease time before natural cure kicks in.</summary>
    [DataField]
    public float MaxDuration { get; private set; } = 300f;

    public override bool Cure(DiseaseEffectArgs args)
    {
        if (!args.EntityManager.TryGetComponent<DiseaseCarrierComponent>(args.DiseasedEntity, out var carrier))
            return false;

        // Check accumulated time for this specific disease
        if (carrier.Diseases.TryGetValue(args.Disease.ID, out var accumulated))
            return accumulated >= MaxDuration;

        return false;
    }
}
