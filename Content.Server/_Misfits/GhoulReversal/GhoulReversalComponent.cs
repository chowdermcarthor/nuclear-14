// #Misfits Change
using Content.Server._Misfits.GhoulReversal;

namespace Content.Server._Misfits.GhoulReversal;

/// <summary>
/// Applied to an injectable that reverses ghouls back to humans.
/// When injected into a Ghoul or GhoulGlowing species, reverts them to Human
/// and optionally updates their database profile so the change persists.
/// </summary>
[RegisterComponent, Access(typeof(GhoulReversalSystem))]
public sealed partial class GhoulReversalComponent : Component
{
    /// <summary>
    /// Whether to update the character's database profile to Human after reversal.
    /// If false, the transformation is only in-game and won't persist across rounds.
    /// </summary>
    [DataField]
    public bool UpdateDatabaseProfile = true;

    /// <summary>
    /// The species IDs that this syringe can reverse. Defaults to both ghoul variants.
    /// </summary>
    [DataField]
    public List<string> GhoulSpecies = new() { "Ghoul", "GhoulGlowing" };

    /// <summary>
    /// The species to revert the target to.
    /// </summary>
    [DataField]
    public string TargetSpecies = "Human";

    /// <summary>
    /// Popup message shown to the injected entity.
    /// </summary>
    [DataField]
    public string TransformationMessage = "ghoul-reversal-self";

    /// <summary>
    /// Popup message shown to others nearby.
    /// </summary>
    [DataField]
    public string TransformationOthersMessage = "ghoul-reversal-others";

    /// <summary>
    /// Popup shown when the target is not a ghoul.
    /// </summary>
    [DataField]
    public string NotGhoulMessage = "ghoul-reversal-not-ghoul";
}
