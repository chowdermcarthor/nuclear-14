using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Burial;

/// <summary>
/// Fired when the grave-digging doAfter completes for a <see cref="Components.GraveCreatorComponent"/>.
/// Carries the spawn coordinates as a network-safe <see cref="NetCoordinates"/> value so that
/// NetSerializer can handle the event without issues (EntityCoordinates is not [Serializable]).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GraveCreationDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates SpawnCoordinates;

    public GraveCreationDoAfterEvent() { }

    public GraveCreationDoAfterEvent(NetCoordinates coords)
    {
        SpawnCoordinates = coords;
    }
}
