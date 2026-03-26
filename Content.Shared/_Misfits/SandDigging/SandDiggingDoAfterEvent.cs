using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.SandDigging;

/// <summary>
/// Fired when the sand-digging doAfter completes for a <see cref="SandDiggerComponent"/>.
/// Carries the dig coordinates so the server can spawn sand at the correct location.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SandDiggingDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates DigCoordinates;

    public SandDiggingDoAfterEvent() { }

    public SandDiggingDoAfterEvent(NetCoordinates coords)
    {
        DigCoordinates = coords;
    }
}
