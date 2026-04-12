// Corvax-Change-Start
using System.Threading;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class TimedSpawnerComponent : Component
{
    [DataField] public List<EntProtoId> Prototypes = [];
    [DataField] public float Chance = 1.0f;
    [DataField] public int IntervalSeconds = 60;
    [DataField] public int MinimumEntitiesSpawned = 1;
    [DataField] public int MaximumEntitiesSpawned = 1;
    /// <summary>
    /// Required for poop spawners.
    /// </summary>
    [DataField] public bool IgnoreSpawnBlock = false;
    public float TimeElapsed;

    // #Misfits Add - Proximity activation: spawner only fires when a living player is within this radius.
    // 0 = disabled (always active), for backwards compatibility with non-N14 spawners.
    [DataField] public float ActivationRange = 0f;

    // #Misfits Add - Map-wide alive NPC population cap per prototype.
    // If alive mobs matching this spawner's prototypes on the same map >= this value, spawning is blocked.
    // 0 = unlimited, for backwards compatibility.
    [DataField] public int MaxAlivePerPrototype = 0;
}
// Corvax-Change-End
