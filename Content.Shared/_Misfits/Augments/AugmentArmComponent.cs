// #Misfits Add - Marker component for cybernetic arm organ entities.
// Ported and adapted from Goob-Station (Content.Goobstation.Shared.Augments.AugmentArmComponent).
// Used by surgery/autosurgeon systems to identify arm slots.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Indicates this organ is a cybernetic arm augment.
/// Companion to AugmentComponent; used to identify arm-slot augments in surgery.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class AugmentArmComponent : Component;
