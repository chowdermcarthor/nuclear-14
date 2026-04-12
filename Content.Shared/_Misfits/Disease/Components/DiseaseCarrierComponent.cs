// #Misfits Add - Disease carrier component.
// Tracks active diseases, past diseases (immunity), and base resistance on humanoid entities.

using Content.Shared._Misfits.Disease;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// Entity can carry diseases. Tracks active infections, past diseases (immunity),
/// and resistance modifiers from equipment/innate traits.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseCarrierComponent : Component
{
    /// <summary>Active disease instances: proto ID → accumulated time in seconds.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DiseasePrototype>, float> Diseases = new();

    /// <summary>Disease IDs this entity has recovered from (grants immunity).</summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<DiseasePrototype>> PastDiseases = new();

    /// <summary>Disease IDs this entity is naturally immune to.</summary>
    [DataField]
    public List<ProtoId<DiseasePrototype>> NaturalImmunities = new();

    /// <summary>
    /// Diseases the entity carries silently (no symptoms, but can still spread).
    /// Used for asymptomatic carriers.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<DiseasePrototype>> CarrierDiseases = new();

    /// <summary>Additive resistance from equipment (gas masks, hazmat suits).</summary>
    [ViewVariables]
    public float DiseaseResist;

    /// <summary>Per-disease accumulator tracking — seconds since last tick.</summary>
    [ViewVariables]
    public Dictionary<string, float> Accumulators = new();
}
