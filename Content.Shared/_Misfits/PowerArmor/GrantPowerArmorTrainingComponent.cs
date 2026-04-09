// #Misfits Add - Marker component placed on items (books/manuals) that grant power armor training when used in hand.
// Handled server-side by GrantPowerArmorTrainingSystem.

using Robust.Shared.Audio;

namespace Content.Shared._Misfits.PowerArmor;

/// <summary>
///     Placed on a manual/book item. When the item is used in hand, the wielder receives
///     <see cref="PowerArmorProficiencyComponent"/> and the item is consumed.
/// </summary>
[RegisterComponent]
public sealed partial class GrantPowerArmorTrainingComponent : Component
{
    /// <summary>Sound played when the manual is successfully read.</summary>
    [DataField]
    public SoundSpecifier? SoundOnUse;
}
