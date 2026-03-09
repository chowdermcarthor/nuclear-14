// #Misfits Change
using Content.Shared.Atmos;

namespace Content.Server._Misfits.Atmos.Components;

/// <summary>
/// Dynamically added to a mob by <see cref="EntitySystems.GasFilterMaskSystem"/> when the mob
/// equips one or more items with <see cref="GasFilterMaskComponent"/>.
/// Tracks which gas types should be filtered from ambient inhalation.
/// </summary>
[RegisterComponent]
public sealed partial class GasFilterActiveComponent : Component
{
    /// <summary>
    /// Maps each currently-equipped filter source to its set of filtered gases.
    /// Recalculated whenever a source is added or removed.
    /// </summary>
    public Dictionary<EntityUid, List<Gas>> ActiveSources = new();

    /// <summary>
    /// The union of all filtered gases across all active sources. Used during inhalation.
    /// </summary>
    public HashSet<Gas> FilteredGases = new();
}
