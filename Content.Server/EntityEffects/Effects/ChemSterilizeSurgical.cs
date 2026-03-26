using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._Misfits.Surgery.Contamination;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Applies surgical contamination cleanup when a sterilizing reagent touches a tool.
/// </summary>
[UsedImplicitly]
public sealed partial class ChemSterilizeSurgical : EntityEffect
{
    [DataField]
    public FixedPoint2 DirtAmount = 10;

    [DataField]
    public int DnaAmount = 1;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var ev = new SurgeryCleanedEvent(DirtAmount, DnaAmount);
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, ref ev);
    }
}