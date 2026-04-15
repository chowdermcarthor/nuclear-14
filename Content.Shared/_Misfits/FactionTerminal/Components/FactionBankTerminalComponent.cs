// #Misfits Add - Component that marks an entity as a faction-specific bank/currency terminal.
// When a player activates the terminal it opens their persistent currency wallet window.
// Position is saved to atm_placements.json so terminals re-spawn automatically each round.
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.FactionTerminal.Components;

/// <summary>
/// Which in-world faction this terminal belongs to.
/// Used for flavour text and later for any faction-gated access rules.
/// </summary>
[Serializable, NetSerializable]
public enum BankFaction : byte
{
    NCR,
    Legion,
    BrotherhoodOfSteel,
    VaultDwellers,
    Townsfolk,
    Wasteland, // #Misfits Add - unaffiliated wastelander banking option
}

/// <summary>
/// Attached to every faction bank terminal entity.
/// Opening the terminal wallet UI is handled entirely by <c>FactionBankTerminalSystem</c>.
/// </summary>
[RegisterComponent] // #Misfits Fix - Removed NetworkedComponent: no AutoGenerateComponentState → MissingMetadataException
public sealed partial class FactionBankTerminalComponent : Component
{
    /// <summary>Which faction owns/maintains this terminal.</summary>
    [DataField(required: true)]
    public BankFaction Faction;

    /// <summary>
    /// Placement key ("MapName:TileX:TileY") assigned by FactionBankTerminalSystem when
    /// the terminal is first placed.  Used during shutdown to locate and remove the
    /// correct record from atm_placements.json without re-querying the transform.
    /// </summary>
    [DataField]
    public string PlacementKey = string.Empty;
}
