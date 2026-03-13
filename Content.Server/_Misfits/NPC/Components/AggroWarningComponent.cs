// #Misfits Add - DISABLED: Aggro warning delay system caused mobs to follow without attacking
// and introduced server lag. Replaced by a simple ping-on-aggro in CombatModePingSystem.
// Kept commented out per project no-deletion policy.

// namespace Content.Server._Misfits.NPC.Components;
//
// /// <summary>
// /// Added to an NPC when it first detects a hostile target. While this component
// /// exists the NPC will not pursue or attack, giving the player time to back off.
// /// </summary>
// [RegisterComponent]
// public sealed partial class AggroWarningComponent : Component
// {
//     /// <summary>
//     /// Seconds remaining in the warning window.
//     /// </summary>
//     [ViewVariables(VVAccess.ReadWrite)]
//     public float TimeRemaining = 2f;
//
//     /// <summary>
//     /// If the target is within this many tiles, skip the delay and attack immediately.
//     /// </summary>
//     [ViewVariables(VVAccess.ReadWrite)]
//     public float InstantAggroRange = 5f;
//
//     /// <summary>
//     /// Whether the warning ping has already been played for this aggro window.
//     /// </summary>
//     [ViewVariables]
//     public bool PingPlayed;
// }
