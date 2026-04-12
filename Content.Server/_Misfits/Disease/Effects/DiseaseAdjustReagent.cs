// #Misfits Add - Disease effect: inject/remove reagent from bloodstream.
// Adds or removes a specified reagent from the entity's chemical solution.

using Content.Shared._Misfits.Disease;
using Content.Server.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;

namespace Content.Server._Misfits.Disease.Effects;

/// <summary>
/// Injects or removes a reagent from the infected entity's bloodstream.
/// Used for diseases that induce toxins or deplete beneficial chemicals.
/// </summary>

public sealed partial class DiseaseAdjustReagent : DiseaseEffect
{
    /// <summary>Reagent prototype ID to inject.</summary>
    [DataField(required: true)]
    public string Reagent { get; private set; } = string.Empty;

    /// <summary>Amount to inject per tick. Negative values remove the reagent.</summary>
    [DataField]
    public float Amount { get; private set; } = 3f;

    public override void Effect(DiseaseEffectArgs args)
    {
        if (!args.EntityManager.TryGetComponent<BloodstreamComponent>(args.DiseasedEntity, out var blood))
            return;

        var solutionSys = args.EntityManager.System<SharedSolutionContainerSystem>();

        // Resolve the chemical (medication) solution on the bloodstream
        if (!solutionSys.ResolveSolution(args.DiseasedEntity, blood.ChemicalSolutionName,
                ref blood.ChemicalSolution, out _))
            return;

        // Positive amount = inject reagent, negative = remove
        if (Amount > 0)
        {
            solutionSys.TryAddReagent(blood.ChemicalSolution.Value, Reagent, FixedPoint2.New(Amount));
        }
        else
        {
            solutionSys.RemoveReagent(blood.ChemicalSolution.Value, Reagent, FixedPoint2.New(-Amount));
        }
    }
}
