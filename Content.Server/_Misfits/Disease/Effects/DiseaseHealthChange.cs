// #Misfits Add - Disease effect: deal typed damage to the afflicted entity.
// Configurable damage types and amounts per tick.

using Content.Shared._Misfits.Disease;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.Disease.Effects;

/// <summary>
/// Deals damage of specified types each tick. Used for diseases that cause
/// direct harm (radiation sickness, poison buildup, organ failure, etc.).
/// </summary>

public sealed partial class DiseaseHealthChange : DiseaseEffect
{
    /// <summary>Damage to apply per tick. Key = damage type ID, value = amount.</summary>
    [DataField(required: true)]
    public Dictionary<string, float> Damage { get; private set; } = new();

    public override void Effect(DiseaseEffectArgs args)
    {
        var damageSpec = new DamageSpecifier();
        foreach (var (type, amount) in Damage)
        {
            damageSpec.DamageDict.Add(type, amount);
        }

        var damageable = args.EntityManager.System<DamageableSystem>();
        damageable.TryChangeDamage(args.DiseasedEntity, damageSpec, ignoreResistances: false);
    }
}
