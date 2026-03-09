// #Misfits Change - Ported from Delta-V surgery contamination system
using Content.Shared.FixedPoint;

namespace Content.Shared._Misfits.Surgery.Contamination;

/// <summary>
///     Event fired when an object is sterilised for surgery.
/// </summary>
[ByRefEvent]
public record struct SurgeryCleanedEvent(FixedPoint2 DirtAmount, int DnaAmount);
