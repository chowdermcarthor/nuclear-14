// Lightweight component that drains the robot's chassis power cell each time
// its built-in gun fires. Used on Gutsy (plasma) and Handy (flamer) where the
// weapon doesn't have a complex charge-up cycle like the Assaultron beam.

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Misfits.Robot;

[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class RobotGunPowerDrainComponent : Component
{
    /// <summary>Charge drained from the cell_slot battery per shot fired.</summary>
    [DataField]
    public float DrainPerShot = 50f;

    /// <summary>ItemSlot ID containing the robot's power cell.</summary>
    [DataField]
    public string CellSlotId = "cell_slot";
}
