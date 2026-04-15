// #Misfits Add - Melee damage multiplier augment component.
// Ported and adapted from Goob-Station (Content.Goobstation.Shared.Augments.AugmentStrengthComponent).
// Works passively or behind an ItemToggleComponent (when deactivated, no bonus applies).
// Fallout flavour: "Hydraulic Frame", "Cybernetic Arm", etc.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Multiplies all melee damage dealt by the body this augment is installed in
/// while the augment is active (or always, if it has no ItemToggleComponent).
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
[Access(typeof(AugmentStrengthSystem))]
public sealed partial class AugmentStrengthComponent : Component
{
    /// <summary>
    /// Damage multiplier applied to every melee swing (1.0 = no change).
    /// </summary>
    [DataField]
    public float Modifier = 1.25f;
}
