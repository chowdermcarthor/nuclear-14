using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Scope;

/// <summary>
/// Gives an entity (gun or attachment item) a scope that shifts the wielder's view forward.
/// - If IsAttachment = false: the component is on the gun itself (built-in scope).
///   A toggle action is granted automatically when the gun is picked up.
/// - If IsAttachment = true: the component is on a standalone scope item that must be
///   inserted into the gun's gun_scope ItemSlot. The action is granted when the gun
///   (containing the scope) is picked up.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class N14ScopeComponent : Component
{
    /// <summary>
    /// How many tiles to offset the viewport forward (toward facing direction) when scoped in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EyeOffset = 5f;

    /// <summary>
    /// If true, this is a standalone attachment item (goes in a gun's scope slot).
    /// The item will not grant an action when held directly — only when slotted into a gun.
    /// </summary>
    [DataField]
    public bool IsAttachment = false;

    /// <summary>
    /// Action entity prototype to grant when the scope-capable gun is held.
    /// </summary>
    [DataField]
    public EntProtoId ToggleActionId = "N14ActionToggleScope";

    /// <summary>Runtime: the spawned action entity for the current user.</summary>
    [ViewVariables]
    public EntityUid? ToggleActionEntity;

    /// <summary>Runtime: whether the scope is currently zoomed in.</summary>
    [DataField, AutoNetworkedField]
    public bool IsActive;

    /// <summary>Runtime: which entity currently has the scope action granted to them.</summary>
    [ViewVariables]
    public EntityUid? CurrentUser;
}
