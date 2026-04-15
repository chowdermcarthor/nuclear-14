// #Misfits Add - Marks an item as valid fuel for campfires and braziers.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Campfire;

/// <summary>
/// Marks an item as valid fuel for campfires. Add it to wood planks, scrap, etc.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class CampfireFuelComponent : Component
{
    /// <summary>
    /// How much fuel this item provides when added to a campfire.
    /// </summary>
    [DataField]
    public int FuelAmount = 1;
}
