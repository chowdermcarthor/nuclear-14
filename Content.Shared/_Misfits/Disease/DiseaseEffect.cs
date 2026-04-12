// #Misfits Add - Abstract base for disease effects.
// Effects run per tick when their stage condition matches the carrier's current stage.

namespace Content.Shared._Misfits.Disease;

/// <summary>
/// Base class for disease effects. Subclasses implement specific behaviors
/// (damage, popups, emotes, status effects, reagent injection, etc.).
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class DiseaseEffect
{
    /// <summary>Probability [0-1] that this effect fires each tick.</summary>
    [DataField]
    public float Probability { get; private set; } = 1f;

    /// <summary>Which stages this effect is active during (0-indexed).</summary>
    [DataField]
    public List<int> Stages { get; private set; } = new() { 0 };

    /// <summary>Execute the effect on the target entity.</summary>
    public abstract void Effect(DiseaseEffectArgs args);
}

/// <summary>
/// Arguments passed to each disease effect when it fires.
/// </summary>
public readonly struct DiseaseEffectArgs
{
    /// <summary>The entity afflicted by the disease.</summary>
    public readonly EntityUid DiseasedEntity;

    /// <summary>The disease prototype that triggered this effect.</summary>
    public readonly DiseasePrototype Disease;

    /// <summary>ECS entity manager reference for queries.</summary>
    public readonly IEntityManager EntityManager;

    public DiseaseEffectArgs(EntityUid diseasedEntity, DiseasePrototype disease, IEntityManager entityManager)
    {
        DiseasedEntity = diseasedEntity;
        Disease = disease;
        EntityManager = entityManager;
    }
}
