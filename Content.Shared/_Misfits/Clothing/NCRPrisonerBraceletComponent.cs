using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Clothing;

/// <summary>
/// Marks an NCR prisoner ankle bracelet as lock-restricted equipment.
/// NCR officers (Lieutenant, Ranger) can remove it via AccessReader. Others may cut it off
/// with wire cutters after a 20-second DoAfter.
/// Crafted bracelets produce a unique paired key that also unlocks them if re-applied.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class NCRPrisonerBraceletComponent : Component
{
    /// <summary>
    /// Crafted bracelets spawn this key prototype, then stamp it with a unique access tag.
    /// </summary>
    [DataField]
    public EntProtoId KeyPrototype = "N14IDKeyNCRPrisonerBracelet";

    /// <summary>
    /// Tool quality required to forcibly cut open a locked bracelet.
    /// </summary>
    [DataField("cutToolQuality")]
    public string CutToolQuality = "Cutting";

    /// <summary>
    /// Duration in seconds for the rescue-cut DoAfter (20 seconds per design spec).
    /// </summary>
    [DataField]
    public float CutUnlockTime = 20f;

    /// <summary>
    /// Prefix used when generating unique runtime access tags for crafted bracelets.
    /// </summary>
    [DataField]
    public string RandomAccessPrefix = "NCRPrisonerBracelet";

    [DataField]
    public int RandomKeyMin = 1000;

    [DataField]
    public int RandomKeyMax = 9999;

    /// <summary>
    /// Set to true once a unique key has been generated so it is not generated again.
    /// </summary>
    [DataField]
    public bool GeneratedKey;
}

/// <summary>
/// Fired when a rescue tool finishes cutting a locked NCR prisoner bracelet.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class NCRPrisonerBraceletCutDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone()
    {
        return this;
    }
}
