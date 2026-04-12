// #Misfits Add - Disease cure: body temperature threshold.
// Cures the disease if the entity's body temperature is above/below a threshold.

using Content.Shared._Misfits.Disease;
using Content.Server.Temperature.Components;

namespace Content.Server._Misfits.Disease.Cures;

/// <summary>
/// Disease is cured when the entity's body temperature crosses a threshold.
/// Can check for high temperature (fever breaking) or low temperature (cooling down).
/// </summary>

public sealed partial class DiseaseBodyTemperatureCure : DiseaseCure
{
    /// <summary>Temperature threshold in Kelvin.</summary>
    [DataField]
    public float Threshold { get; private set; } = 310f;

    /// <summary>If true, cure when temperature is ABOVE threshold. If false, cure when BELOW.</summary>
    [DataField]
    public bool MustBeAbove { get; private set; } = true;

    public override bool Cure(DiseaseEffectArgs args)
    {
        if (!args.EntityManager.TryGetComponent<TemperatureComponent>(args.DiseasedEntity, out var temp))
            return false;

        return MustBeAbove
            ? temp.CurrentTemperature >= Threshold
            : temp.CurrentTemperature <= Threshold;
    }
}
