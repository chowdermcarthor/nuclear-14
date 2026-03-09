// #Misfits Change - Ported from Delta-V surgery contamination system
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Surgery.Contamination;

/// <summary>
///     Handled by the server when a surgery step is completed in order to deal with sanitization concerns.
/// </summary>
[ByRefEvent]
public record struct SurgeryDirtinessEvent(EntityUid User, EntityUid Part, List<EntityUid> Tools, EntityUid Step);
