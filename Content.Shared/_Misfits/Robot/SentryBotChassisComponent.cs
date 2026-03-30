// Marker component for player-controlled Sentry Bot chassis.
// Presence on an entity triggers the SentryBotOverheatSystem to monitor ammo depletion
// and the SentryBotMissileLauncherSystem to grant the missile launch action.

using Content.Shared.Actions;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Misfits.Robot;

/// <summary>
/// Marks this entity as a player-controlled Sentry Bot chassis.
/// Enables overheat cooldown announcements when the main weapon runs dry,
/// and grants the missile launch action on initialisation.
/// </summary>
[RegisterComponent]
public sealed partial class SentryBotChassisComponent : Component
{
    /// <summary>
    /// How long (in seconds) the weapon stays locked out after ammo depletes.
    /// During this period the gun cannot fire and the overheat message is shown.
    /// </summary>
    [DataField]
    public float OverheatDuration = 5f;

    /// <summary>
    /// Whether the sentry bot is currently in an overheat cooldown.
    /// </summary>
    [ViewVariables]
    public bool Overheating;

    /// <summary>
    /// When the current overheat cooldown ends.
    /// </summary>
    [ViewVariables]
    public TimeSpan OverheatEndTime = TimeSpan.Zero;

    /// <summary>
    /// Stores the spawned missile launch action entity so the system
    /// can reference it after it has been granted.
    /// </summary>
    [DataField]
    public EntityUid? MissileLaunchActionEntity;
}
