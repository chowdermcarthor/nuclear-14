using Robust.Shared.Audio;
using Robust.Shared.GameStates;


namespace Content.Shared._Misfits.Warhorn.Components;


[RegisterComponent, NetworkedComponent]
public sealed partial class WarhornComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound;

    [DataField]
    public float Range = 45f;

    [DataField]
    public float Volume = -4f;
}
