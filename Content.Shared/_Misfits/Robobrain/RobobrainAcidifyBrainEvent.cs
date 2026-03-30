// Event raised when a player uses the "Acidify Brain" action on a Robobrain chassis.
// Must be in Content.Shared so the YAML prototype serializer can resolve it on both
// client and server (entity prototypes reference this type via !type:RobobrainAcidifyBrainEvent).

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Robobrain;

/// <summary>
/// Instant action event raised when the player activates the Acidify Brain action
/// on a player-controlled Robobrain chassis. Handled server-side by
/// <c>Content.Server._Misfits.Robobrain.RobobrainAcidifySystem</c>.
/// </summary>
public sealed partial class RobobrainAcidifyBrainEvent : InstantActionEvent { }
