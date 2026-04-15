// #Misfits Change /Add:/ Temporary damage-resistance status used by Med-X style chems.
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.DrugEffects;

/// <summary>
///     Applies a temporary damage modifier set while the status effect is active.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class MedXProtectionComponent : Component
{
    [DataField("modifier")]
    public ProtoId<DamageModifierSetPrototype> Modifier = "N14MedXProtection";
}