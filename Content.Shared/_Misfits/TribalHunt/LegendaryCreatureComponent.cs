using Robust.Shared.GameObjects;
namespace Content.Shared._Misfits.TribalHunt;
using Robust.Shared.GameStates;

/// <summary>
/// Marks an entity as a Legendary creature spawned during tribal hunts.
/// Tracks creature state for hunt completion and loot coordination.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LegendaryCreatureComponent : Component
{
    /// <summary>
    /// The hunt session entity ID that spawned this creature.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? HuntSessionId = null;

    /// <summary>
    /// Creature name for notifications and indicators.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public string CreatureName = "Legendary Beast";

    /// <summary>
    /// Number of legendary leather drops on death.
    /// </summary>
    [DataField]
    public int LeatherDropCount = 3;

    /// <summary>
    /// Whether creature location should be revealed to tribe members.
    /// </summary>
    [DataField]
    public bool RevealLocation = true;

}
