// #Misfits Change - Ported from Delta-V anesthesia system
using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Surgery.Anesthesia;

/// <summary>
///     Exists as a status effect. When present, surgical operations cause reduced pain and screaming.
///     Can be applied by anesthetic reagents or specialized operating tables.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AnesthesiaComponent : Component;
