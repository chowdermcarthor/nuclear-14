// #Misfits Change
namespace Content.Server._Misfits.RadiationWarning;

/// <summary>
/// Tracks per-entity radiation warning cooldowns.
/// Dynamically added to humanoids the first time they receive radiation.
/// </summary>
[RegisterComponent]
public sealed partial class RadiationWarningComponent : Component
{
    /// <summary>
    /// Seconds since the last ambient-tier message was sent.
    /// Each tier has its own cooldown tracked independently.
    /// Index 0 = mild, 1 = moderate, 2 = significant, 3 = severe.
    /// </summary>
    public float[] TierCooldowns = new float[4];

    /// <summary>
    /// Minimum seconds between messages per tier.
    /// </summary>
    [DataField]
    public float[] TierCooldownTimes = { 75f, 55f, 35f, 22f };

    /// <summary>
    /// RadsPerSecond thresholds that unlock each tier.
    /// </summary>
    [DataField]
    public float[] TierThresholds = { 0.05f, 0.4f, 1.0f, 2.5f };
}
