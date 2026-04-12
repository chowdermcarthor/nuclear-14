// #Misfits Add - Wasteland Disease prototype definition.
// Defines a YAML-configurable disease with staged effects, cures, and spread behavior.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Misfits.Disease;

/// <summary>
/// Prototype for a disease. Defines infection behavior, stage thresholds,
/// effects (damage, status, emotes) and cures (reagent, bedrest, time, etc.).
/// </summary>
[Prototype("disease")]
public sealed partial class DiseasePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Localized display name key.</summary>
    [DataField]
    public LocId Name { get; private set; } = string.Empty;

    /// <summary>Whether the disease can spread to other carriers.</summary>
    [DataField]
    public bool Infectious { get; private set; } = true;

    /// <summary>Spreads by sneeze/cough to nearby entities.</summary>
    [DataField]
    public bool Airborne { get; private set; } = true;

    /// <summary>Chance of spreading via physical contact (touch interaction). 0-1.</summary>
    [DataField]
    public float ContactSpread { get; private set; } = 0.3f;

    /// <summary>Base resistance subtracted from infection chance roll.</summary>
    [DataField]
    public float CureResist { get; private set; }

    /// <summary>Seconds between effect ticks.</summary>
    [DataField]
    public float TickTime { get; private set; } = 3f;

    /// <summary>
    /// Stage thresholds in seconds. Index = stage number.
    /// Stage 0 starts at 0s. Each entry marks when the next stage begins.
    /// </summary>
    [DataField]
    public List<float> Stages { get; private set; } = new() { 0f };

    /// <summary>Disease effects (damage, popups, emotes, status effects).</summary>
    [DataField]
    public List<DiseaseEffect> Effects { get; private set; } = new();

    /// <summary>Cure conditions checked each tick.</summary>
    [DataField]
    public List<DiseaseCure> Cures { get; private set; } = new();
}
