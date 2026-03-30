// Marker component for the player-controlled Robobrain chassis.
// Presence on an entity causes RobobrainAcidifySystem to grant the
// Acidify Brain instant action on component initialisation.

using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Robobrain;

/// <summary>
/// Marks this entity as a player-controlled Robobrain chassis.
/// Triggers intrinsic action grants and Robobrain-specific behaviour
/// in <see cref="RobobrainAcidifySystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class RobobrainChassisComponent : Component
{
    /// <summary>
    /// Stores the spawned Acidify Brain action entity so the system
    /// can reference it after it has been granted.
    /// </summary>
    [DataField]
    public EntityUid? AcidifyActionEntity;
}
