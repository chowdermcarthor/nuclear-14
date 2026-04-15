// #Misfits Change Add: Marks a shield or hardsuit as capable of deflecting thrown Spear-tagged weapons.
// When worn or held, SpearBlockSystem propagates SpearBlockUserComponent to the equipee.
// The spear does not embed; falls to the ground and shows a narrative popup.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Throwing.Components;

[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class SpearBlockComponent : Component
{
}
