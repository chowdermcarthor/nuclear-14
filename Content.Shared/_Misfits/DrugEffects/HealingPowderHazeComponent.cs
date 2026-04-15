// #Misfits Change /Add:/ Healing powder visual status effect
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.DrugEffects;

/// <summary>
///     Status effect marker for a mild healing powder haze overlay.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class HealingPowderHazeComponent : Component
{
}