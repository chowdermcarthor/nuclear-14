// #Misfits Change
namespace Content.Server._Misfits.GhoulReversal;

/// <summary>
/// Stamped onto an entity when they are ghoulified mid-round via radiation.
/// Stores the real UTC time of ghoulification so that reagent-based reversal
/// can enforce a 12-hour window.
/// Round-start ghoul characters will NOT have this component, meaning
/// the reagent reversal will be blocked for them (they chose that life).
/// The admin syringe (GhoulReversalComponent) bypasses this entirely.
/// </summary>
[RegisterComponent]
public sealed partial class GhoulificationTimeComponent : Component
{
    /// <summary>
    /// The real-world UTC time at which this entity was ghoulified.
    /// </summary>
    public DateTime GhoulifiedAtUtc = DateTime.UtcNow;

    /// <summary>
    /// Maximum window (in real hours) during which reagent reversal is permitted.
    /// After this time, the ghoulification is considered permanent and irreversible via chemistry.
    /// </summary>
    [DataField]
    public double ReversibleWindowHours = 12.0;
}
