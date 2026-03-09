// #Misfits Change
namespace Content.Server._Misfits.GhoulReversal;

/// <summary>
/// When present on a humanoid, causes them to transform into a Ghoul and
/// revive upon dying to radiation damage instead of fully dying.
/// Intended for human characters exposed to MarkerRadiation environmental sources.
/// The transformation is tracked by GhoulificationTimeComponent so Promethine chemistry can reverse it.
/// </summary>
[RegisterComponent]
public sealed partial class GhoulifyOnRadiationDeathComponent : Component
{
    /// <summary>
    /// Minimum accumulated radiation damage (in the Damageable component) required
    /// at time of death to trigger ghoulification. Prevents minor rad ticks from
    /// triggering the effect when something else actually killed the player.
    /// </summary>
    [DataField]
    public float MinimumRadiationDamage = 30f;

    /// <summary>
    /// Species to transform into on radiation death.
    /// </summary>
    [DataField]
    public string GhoulSpecies = "Ghoul";
}
