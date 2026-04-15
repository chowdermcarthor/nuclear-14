// #Misfits Change Add: Added to mobs when they equip or hold an item with SpearBlockComponent.
// Enables SpearBlockSystem to receive ThrowHitByEvent and deflect Spear-tagged weapons.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Throwing.Components;

[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class SpearBlockUserComponent : Component
{
}
