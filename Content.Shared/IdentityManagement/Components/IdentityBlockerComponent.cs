using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization; // #Misfits Fix - Required for enum serialization attributes

namespace Content.Shared.IdentityManagement.Components;

// #Misfits Fix - Add AutoGenerateComponentState so Enabled field syncs to clients after mask toggle
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IdentityBlockerComponent : Component
{
    [DataField, AutoNetworkedField] // #Misfits Fix - AutoNetworkedField ensures Enabled changes propagate to clients
    public bool Enabled = true;

    /// <summary>
    /// What part of your face does this cover? Eyes, mouth, or full?
    /// </summary>
    [DataField]
    public IdentityBlockerCoverage Coverage = IdentityBlockerCoverage.FULL;
}

// #Misfits Fix - Add Flags and NetSerializable for proper bitwise operations and network serialization
[Flags]
[Serializable, NetSerializable]
public enum IdentityBlockerCoverage
{
    NONE  = 0,
    MOUTH = 1 << 0,
    EYES  = 1 << 1,
    OUTER = 1 << 2,
    FULL  = MOUTH | EYES | OUTER
}

/// <summary>
///     Raised on an entity and relayed to inventory to determine if its identity should be knowable.
/// </summary>
public sealed class SeeIdentityAttemptEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    // i.e. masks, helmets, or glasses.
    public SlotFlags TargetSlots => SlotFlags.MASK | SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.OUTERCLOTHING;

    // cumulative coverage from each relayed slot
    public IdentityBlockerCoverage TotalCoverage = IdentityBlockerCoverage.NONE;
}
