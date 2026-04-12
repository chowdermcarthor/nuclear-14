// #Misfits Add - Disease cure: reagent in bloodstream.
// Cures the disease if the entity has enough of a specific reagent in their blood.

using Content.Shared._Misfits.Disease;
using Content.Server.Body.Components;
using Content.Shared.Chemistry.EntitySystems;

namespace Content.Server._Misfits.Disease.Cures;

/// <summary>
/// Disease is cured when the entity has a minimum amount of a specific reagent
/// in their bloodstream chemistry solution.
/// </summary>

public sealed partial class DiseaseReagentCure : DiseaseCure
{
    /// <summary>Reagent prototype ID to check for.</summary>
    [DataField(required: true)]
    public string Reagent { get; private set; } = string.Empty;

    /// <summary>Minimum amount of reagent required in bloodstream.</summary>
    [DataField]
    public float MinAmount { get; private set; } = 5f;

    public override bool Cure(DiseaseEffectArgs args)
    {
        if (!args.EntityManager.TryGetComponent<BloodstreamComponent>(args.DiseasedEntity, out var blood))
            return false;

        var solutionSys = args.EntityManager.System<SharedSolutionContainerSystem>();
        if (!solutionSys.ResolveSolution(args.DiseasedEntity, blood.ChemicalSolutionName,
                ref blood.ChemicalSolution, out var solution))
            return false;

        // Check if the reagent exists in sufficient quantity in the chemical solution
        foreach (var (reagentId, quantity) in solution.Contents)
        {
            if (reagentId.Prototype == Reagent && quantity.Float() >= MinAmount)
                return true;
        }

        return false;
    }
}
