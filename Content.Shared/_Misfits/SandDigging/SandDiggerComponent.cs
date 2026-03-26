using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.SandDigging;

/// <summary>
/// When placed on a shovel-type item, enables digging sand from desert tiles.
/// The system checks the tile at the click location against <see cref="SandTileIds"/>
/// and, on success, spawns <see cref="SandPrototype"/> at the dig site.
/// </summary>
[RegisterComponent]
public sealed partial class SandDiggerComponent : Component
{
    /// <summary>
    /// Tile definition IDs that count as "sand" and can be dug for a sand pile.
    /// </summary>
    [DataField]
    public HashSet<string> SandTileIds = new()
    {
        "FloorDesert",
        "FloorLowDesert",
    };

    /// <summary>
    /// Entity prototype spawned when sand is successfully dug.
    /// </summary>
    [DataField]
    public EntProtoId SandPrototype = "N14Sand1";

    /// <summary>
    /// Base time to dig sand before any speed modifiers.
    /// </summary>
    [DataField]
    public TimeSpan DigDelay = TimeSpan.FromSeconds(4f);

    /// <summary>
    /// Looping sound played while digging sand.
    /// </summary>
    [DataField]
    public SoundPathSpecifier DigSound = new("/Audio/Nyanotrasen/Items/shovel_dig.ogg")
    {
        Params = AudioParams.Default.WithLoop(true)
    };

    /// <summary>
    /// Currently-playing looping audio stream entity. Null when idle.
    /// </summary>
    [DataField]
    public EntityUid? Stream;

    /// <summary>
    /// True while a sand-digging doAfter is in progress.
    /// </summary>
    [DataField]
    public bool IsDigging;
}
