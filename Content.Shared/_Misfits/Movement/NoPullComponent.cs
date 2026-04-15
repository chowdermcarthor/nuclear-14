// #Misfits Change /Add/ - Prevents an entity from being pulled/dragged by players or NPCs.
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Movement;

/// <summary>
/// When present, this entity cannot be dragged or pulled by anyone.
/// The system subscribes to <see cref="Content.Shared.Movement.Pulling.Events.BeingPulledAttemptEvent"/>
/// and cancels it. Intended for heavy robots such as the Sentry Bot.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class NoPullComponent : Component
{
}
