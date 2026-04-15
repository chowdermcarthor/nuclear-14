// #Misfits Add - BarricadeComponent: marker component placed on deployed barricades.
// Used to identify barricade structures for damage/repair tracking.
// Ported from RMC-14, stripped of marine-specific logic.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Entrenching;

[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class BarricadeComponent : Component
{
    // Marker — logic lives in BarricadeSystem
}
