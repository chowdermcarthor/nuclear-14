// #Misfits Add - Disease cure: bedrest.
// Cures the disease if the entity is buckled to something (lying in a bed).

using Content.Shared._Misfits.Disease;
using Content.Shared.Buckle.Components;

namespace Content.Server._Misfits.Disease.Cures;

/// <summary>
/// Disease is cured when the entity is buckled to furniture (bed rest).
/// Represents the Fallout theme of resting to recover from illness.
/// </summary>

public sealed partial class DiseaseBedrestCure : DiseaseCure
{
    public override bool Cure(DiseaseEffectArgs args)
    {
        // Entity must be buckled (strapped to a bed/chair/sleeping bag)
        return args.EntityManager.TryGetComponent<BuckleComponent>(args.DiseasedEntity, out var buckle)
               && buckle.Buckled;
    }
}
