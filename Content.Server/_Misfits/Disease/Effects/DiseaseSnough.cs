// #Misfits Add - Disease effect: trigger sneeze or cough emote.
// Spreads the disease to nearby entities if the carrier isn't wearing a mask.

using Content.Shared._Misfits.Disease;

namespace Content.Server._Misfits.Disease.Effects;

/// <summary>
/// Forces the entity to sneeze or cough via the emote system.
/// If the disease is airborne and the entity has no mask, nearby
/// DiseaseCarrier entities within range may be infected.
/// </summary>

public sealed partial class DiseaseSnough : DiseaseEffect
{
    /// <summary>Emote prototype ID to trigger ("Sneeze" or "Cough").</summary>
    [DataField]
    public string EmoteId { get; private set; } = "Cough";

    /// <summary>Whether this snough can spread the disease to nearby entities.</summary>
    [DataField]
    public bool AirTransmit { get; private set; } = true;

    public override void Effect(DiseaseEffectArgs args)
    {
        // Actual logic handled by DiseaseSystem.SneezeCough() since it needs
        // multiple system dependencies (ChatSystem, EntityLookup, InventorySystem).
        // This effect class just stores configuration; the system reads it.
        // The DiseaseSystem checks for DiseaseSnough effects and calls SneezeCough().
    }
}
