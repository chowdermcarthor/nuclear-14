// #Misfits Add - Procedural ore cluster spawner for planet maps (e.g. Wendover).
// Place the marker entity in the map editor once per mining zone; the system
// handles all rock placement at round start and then removes itself.

using Robust.Shared.Prototypes;

namespace Content.Server._Misfits.OreCluster;

/// <summary>
/// Marker component for a procedural ore cluster. On MapInit, generates a
/// random-shaped blob of <see cref="WallEntity"/> on unoccupied tiles around
/// the spawner's position, then deletes itself. Each round produces a unique
/// layout because the radius and per-tile shape roll are re-seeded each load.
/// </summary>
[RegisterComponent]
public sealed partial class MisfitsOreClusterSpawnerComponent : Component
{
    /// <summary>
    /// Entity prototype to spawn on each eligible tile.
    /// Should be a Mining-variant rock with OreVein + oreRarityPrototypeId
    /// so per-ore randomisation still happens at MapInit.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId WallEntity = "N14WallRockMammothSlanted"; // Misfits Tweak - switched from DroughtSlanted to MammothSlanted for visual consistency

    /// <summary>
    /// Minimum blob radius in tiles.
    /// </summary>
    [DataField]
    public int MinRadius = 4;

    /// <summary>
    /// Maximum blob radius in tiles. Yields a cluster up to (2*MaxRadius+1)^2
    /// tiles in area — default 9 gives a max 19x19 footprint before shape/density
    /// trimming, matching the "no bigger than 18×18" requirement.
    /// </summary>
    [DataField]
    public int MaxRadius = 9;

    /// <summary>
    /// Probability that any in-shape tile actually gets a rock.
    /// Lower values produce sparser, more natural-looking clusters.
    /// </summary>
    [DataField]
    public float FillChance = 0.65f;
}
