// #Misfits Change - Ported from Delta-V anesthesia system
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Surgery.Anesthesia;

/// <summary>
///     Exists for strap entities (operating tables) to apply surgical anesthesia to a patient upon being buckled.
///     Removes the anesthesia when the patient unbuckles unless they already had it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnesthesiaOnBuckleComponent : Component
{
    /// <summary>
    ///     Whether the buckled entity already had anesthesia before being strapped in.
    ///     Used to decide whether to remove anesthesia on unstrap.
    /// </summary>
    [DataField]
    public bool HadAnesthesia;
}
