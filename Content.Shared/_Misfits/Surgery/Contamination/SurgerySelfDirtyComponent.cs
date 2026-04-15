// #Misfits Change - Ported from Delta-V surgery contamination system
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Surgery.Contamination;

/// <summary>
///     Marker component that indicates the entity should become dirtied instead of its tools during surgery.
///     Used for patients with cybernetic or synthetic parts that self-contaminate.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class SurgerySelfDirtyComponent : Component;
