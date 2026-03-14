// #Misfits Add - Far gunshot system. Plays a distant "boom" sound for players far from gunfire.
// Inspired by a similar concept in stalker-14-EN, built independently.
using Robust.Shared.Audio;

namespace Content.Server._Misfits.Weapons.FarGunshot;

/// <summary>
/// Add to any gun entity to enable a distant gunshot sound for players far from the shooter.
/// Players within <see cref="MinDistance"/> tiles hear the normal PVS sound;
/// players between MinDistance and MaxDistance hear this distant crack/boom instead.
/// </summary>
[RegisterComponent]
public sealed partial class FarGunshotComponent : Component
{
    /// <summary>
    /// Minimum tile distance before the far sound begins playing for a listener.
    /// Below this, the normal PVS gunshot covers it.
    /// </summary>
    [DataField]
    public float MinDistance = 10f; // #Misfits Tweak - reduced from 20 to keep far sounds closer-range

    /// <summary>
    /// Maximum tile distance at which any far sound is heard at all.
    /// </summary>
    [DataField]
    public float MaxDistance = 50f; // #Misfits Tweak - reduced from 200; 200 tiles was way too far

    /// <summary>
    /// Optional override for the distant sound.
    /// If null, falls back to the system default (explosionsmallfar.ogg).
    /// </summary>
    [DataField]
    public SoundSpecifier? FarSound = null;

    /// <summary>
    /// If true, no far gunshot plays — use this for suppressed or silenced guns.
    /// </summary>
    [DataField]
    public bool Suppressed = false;
}
