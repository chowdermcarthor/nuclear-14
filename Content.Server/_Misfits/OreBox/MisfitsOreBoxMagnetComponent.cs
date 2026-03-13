// #Misfits Add — Component that gives an anchored OreBox a passive area ore-magnet,
// mirroring OreBag's MagnetPickup but without requiring an inventory slot.
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Misfits.OreBox;

/// <summary>
/// When added to an OreBox (or any anchored Storage entity), periodically
/// pulls nearby loose ore items into storage — no belt slot required.
/// </summary>
[RegisterComponent]
public sealed partial class MisfitsOreBoxMagnetComponent : Component
{
    /// <summary>How often (seconds) to scan for nearby ores.</summary>
    [DataField]
    public float ScanInterval = 1f;

    /// <summary>Radius (tiles) to sweep for loose ores.</summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>Internal timer accumulator.</summary>
    [DataField]
    public float Accumulator = 0f;
}
