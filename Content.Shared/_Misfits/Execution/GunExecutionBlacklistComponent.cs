// #Misfits Add - Blacklists specific guns from the execution verb.
// Ported and adapted from Goob-Station (Content.Goobstation.Shared.Execution).
// Add this to any GunComponent entity that should NOT allow executions
// (e.g. toy guns, nail guns, or other flavour-inappropriate weapons).

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Execution;

/// <summary>
/// Prevents this gun from being used to perform a point-blank execution.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class GunExecutionBlacklistComponent : Component;
