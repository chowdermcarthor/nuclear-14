// #Misfits Add - Disease effect: apply a status effect (Jitter, TemporaryBlindness, etc.).

using Content.Shared._Misfits.Disease;
using Content.Shared.StatusEffect;

namespace Content.Server._Misfits.Disease.Effects;

/// <summary>
/// Applies a named status effect to the diseased entity for a specified duration.
/// Uses the existing StatusEffectsSystem infrastructure.
/// </summary>

public sealed partial class DiseaseGenericStatusEffect : DiseaseEffect
{
    /// <summary>Status effect key (e.g., "Jitter", "TemporaryBlindness", "Stutter").</summary>
    [DataField(required: true)]
    public string Key { get; private set; } = string.Empty;

    /// <summary>How long the status effect lasts in seconds.</summary>
    [DataField]
    public float Duration { get; private set; } = 5f;

    /// <summary>Whether to refresh the timer if the effect is already active.</summary>
    [DataField]
    public bool Refresh { get; private set; } = true;

    /// <summary>Optional component name to add alongside the status effect.</summary>
    [DataField]
    public string? Component { get; private set; }

    public override void Effect(DiseaseEffectArgs args)
    {
        var statusSys = args.EntityManager.System<StatusEffectsSystem>();
        var time = TimeSpan.FromSeconds(Duration);

        // Use the overload with component name only if one is specified
        if (Component != null)
            statusSys.TryAddStatusEffect(args.DiseasedEntity, Key, time, Refresh, Component);
        else
            statusSys.TryAddStatusEffect(args.DiseasedEntity, Key, time, Refresh);
    }
}
