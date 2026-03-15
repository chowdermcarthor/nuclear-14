using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Misfits.Construction;

/// <summary>
/// Client-side system that checks whether a hand-crafting recipe can be fulfilled
/// from items the player currently holds, wears, stores in bags, or has nearby on the ground.
/// Used to populate the "Craftable Now" section in the construction (G-key) menu.
/// </summary>
public sealed class MisfitsCraftableNowSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    // Radius (tiles) within which loose ground items are considered reachable.
    private const float GroundScanRange = 1.5f;

    /// <summary>
    /// Returns <c>true</c> when every <see cref="MaterialConstructionGraphStep"/> on the path
    /// from <paramref name="proto"/>.StartNode to TargetNode can be fulfilled by stacks that
    /// <paramref name="player"/> currently has access to (containers, bags, nearby ground).
    /// Recipes whose paths contain no material steps always return <c>false</c> to avoid
    /// cluttering the list with structure-placement or tool-only recipes.
    /// </summary>
    public bool IsCraftable(ConstructionPrototype proto, EntityUid player)
    {
        if (!_proto.TryIndex<ConstructionGraphPrototype>(proto.Graph, out var graph))
            return false;

        if (!graph.TryPath(proto.StartNode, proto.TargetNode, out var path) || path.Length == 0)
            return false;

        // Walk every edge along the path and sum required material stack counts.
        var required = new Dictionary<string, int>();
        var currentNodeName = proto.StartNode;
        foreach (var node in path)
        {
            var edge = graph.Edge(currentNodeName, node.Name);
            if (edge == null)
                return false;

            foreach (var step in edge.Steps)
            {
                if (step is not MaterialConstructionGraphStep matStep)
                    continue;
                required.TryGetValue(matStep.MaterialPrototypeId, out var prev);
                required[matStep.MaterialPrototypeId] = prev + matStep.Amount;
            }

            currentNodeName = node.Name;
        }

        // Skip recipes with no material steps (structure ghosts, tool-only recipes, etc.).
        if (required.Count == 0)
            return false;

        // Collect available stacks from the player's containers (hands, pockets, backpack, bags…).
        var available = new Dictionary<string, int>();
        var visited = new HashSet<EntityUid>();

        if (TryComp<ContainerManagerComponent>(player, out var playerContainers))
        {
            foreach (var container in playerContainers.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                    CollectStacks(contained, available, visited);
            }
        }

        // Also scan loose items on the ground within interaction range.
        if (TryComp<TransformComponent>(player, out var xform))
        {
            foreach (var (groundEnt, _) in _lookup.GetEntitiesInRange<StackComponent>(
                         xform.Coordinates, GroundScanRange, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (!visited.Contains(groundEnt))
                    CollectStacks(groundEnt, available, visited);
            }
        }

        // All requirements must be satisfied.
        foreach (var (matId, needed) in required)
        {
            available.TryGetValue(matId, out var have);
            if (have < needed)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Recursively tallies <see cref="StackComponent"/> counts accessible from <paramref name="uid"/>,
    /// including contents of any containers it holds (bags, pouches, etc.).
    /// </summary>
    private void CollectStacks(EntityUid uid, Dictionary<string, int> available, HashSet<EntityUid> visited)
    {
        if (!visited.Add(uid))
            return;

        if (TryComp<StackComponent>(uid, out var stack))
        {
            available.TryGetValue(stack.StackTypeId, out var current);
            available[stack.StackTypeId] = current + stack.Count;
        }

        // Recurse into any containers this entity holds (bags, backpacks, etc.).
        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in containers.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
                CollectStacks(contained, available, visited);
        }
    }
}
