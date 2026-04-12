// #Misfits Add - Disease protection component for clothing/equipment.
// Reduces infection chance when worn (gas masks, hazmat suits, etc.).

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Disease.Components;

/// <summary>
/// When equipped, adds its Protection value to the wearer's DiseaseCarrier.DiseaseResist.
/// Used on gas masks, hazmat suits, and other protective gear.
/// </summary>
[RegisterComponent]
public sealed partial class DiseaseProtectionComponent : Component
{
    /// <summary>Flat resistance added to the wearer (0.0-1.0 scale).</summary>
    [DataField]
    public float Protection = 0.15f;

    /// <summary>Whether this protection is currently active (equipped in correct slot).</summary>
    [ViewVariables]
    public bool IsActive;
}
