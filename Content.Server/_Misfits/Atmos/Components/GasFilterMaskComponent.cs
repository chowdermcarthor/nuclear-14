// #Misfits Change
using Content.Shared.Atmos;
using Content.Shared.Inventory;

namespace Content.Server._Misfits.Atmos.Components;

/// <summary>
/// Placed on a clothing item (mask or helmet) to filter harmful gases from the wearer's inhaled air,
/// without requiring a connected gas tank. The mask absorbs the specified gas types so the wearer
/// only breathes the remaining safe portion of ambient atmosphere.
/// </summary>
[RegisterComponent]
[ComponentProtoName("GasFilterMask")]
public sealed partial class GasFilterMaskComponent : Component
{
    /// <summary>
    /// The set of gases this mask filters out. Defaults to common atmos hazards.
    /// </summary>
    [DataField]
    public List<Gas> FilteredGases = new()
    {
        Gas.Plasma,
        Gas.Tritium,
        Gas.Ammonia,
        Gas.NitrousOxide,
        Gas.Frezon,
        Gas.CarbonDioxide,
    };

    /// <summary>
    /// Which inventory slots activate the filter. Defaults to mask and head slots.
    /// </summary>
    [DataField]
    public SlotFlags AllowedSlots = SlotFlags.MASK | SlotFlags.HEAD;
}
