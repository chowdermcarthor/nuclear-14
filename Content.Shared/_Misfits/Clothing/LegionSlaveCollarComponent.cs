using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Clothing;

/// <summary>
/// Marks a slave collar as lock-restricted equipment with rescue-cut and crafted-key behavior.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class LegionSlaveCollarComponent : Component
{
    /// <summary>
    /// Crafted collars spawn this key prototype and stamp it with a unique access tag.
    /// </summary>
    [DataField]
    public EntProtoId KeyPrototype = "N14IDKeyLegionSlaveCollar";

    /// <summary>
    /// Tool quality that can forcibly cut open a locked collar for rescue.
    /// </summary>
    [DataField("cutToolQuality")]
    public string CutToolQuality = "Cutting";

    /// <summary>
    /// Time required to cut open a locked collar (20 seconds per design spec).
    /// </summary>
    [DataField]
    public float CutUnlockTime = 20f;

    /// <summary>
    /// Prefix used when generating unique runtime access tags for crafted collars.
    /// </summary>
    [DataField]
    public string RandomAccessPrefix = "LegionSlaveCollar";

    [DataField]
    public int RandomKeyMin = 1000;

    [DataField]
    public int RandomKeyMax = 9999;

    [DataField]
    public bool GeneratedKey;
}

/// <summary>
/// Fired when a rescue tool finishes cutting a locked slave collar.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class LegionSlaveCollarCutDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone()
    {
        return this;
    }
}
