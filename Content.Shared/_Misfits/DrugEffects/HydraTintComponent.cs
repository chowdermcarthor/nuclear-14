// #Misfits Change /Add:/ Hydra visual status effect
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.DrugEffects;

/// <summary>
///     Status effect marker for the hydra red-screen overlay.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class HydraTintComponent : Component
{
}