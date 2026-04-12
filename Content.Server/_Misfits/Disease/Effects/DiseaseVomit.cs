// #Misfits Add - Disease effect: trigger vomiting on the afflicted entity.

using Content.Server.Medical;
using Content.Shared._Misfits.Disease;

namespace Content.Server._Misfits.Disease.Effects;

/// <summary>
/// Triggers the VomitSystem on the diseased entity, causing them to vomit
/// (hunger/thirst loss, stun, puddle creation).
/// </summary>

public sealed partial class DiseaseVomit : DiseaseEffect
{
    /// <summary>Thirst penalty from vomiting.</summary>
    [DataField]
    public float ThirstAmount { get; private set; } = -40f;

    /// <summary>Hunger penalty from vomiting.</summary>
    [DataField]
    public float HungerAmount { get; private set; } = -40f;

    public override void Effect(DiseaseEffectArgs args)
    {
        var vomit = args.EntityManager.System<VomitSystem>();
        vomit.Vomit(args.DiseasedEntity, ThirstAmount, HungerAmount);
    }
}
