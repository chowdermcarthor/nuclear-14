// #Misfits Add - Core augment/cybernetics component markers.
// Ported and adapted from Goob-Station (Content.Goobstation.Shared.Augments).
// Augments are implemented as Organ entities installed via the body-organ system.
// When an organ with AugmentComponent is installed in a body, it's tracked by
// InstalledAugmentsComponent on the body entity.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Augments;

/// <summary>
/// Marks an organ entity as a cybernetic augment.
/// The body containing this organ can be found via <see cref="AugmentSystem.GetBody"/>.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class AugmentComponent : Component;

/// <summary>
/// Tracks all augments currently installed in this body.
/// Added/removed automatically by AugmentSystem when organs are added/removed.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class InstalledAugmentsComponent : Component
{
    /// <summary>
    /// NetEntity IDs of every installed augment organ.
    /// </summary>
    [DataField]
    public HashSet<NetEntity> InstalledAugments = new();
}
