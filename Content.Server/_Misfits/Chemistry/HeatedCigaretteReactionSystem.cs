using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Temperature;

namespace Content.Server._Misfits.Chemistry;

/// <summary>
/// #Misfits Change
/// Heats solution containers on any entity that reports as hot/lit via IsHotEvent,
/// allowing normal minTemp chemistry to run inside heated containers.
/// </summary>
public sealed class HeatedCigaretteReactionSystem : EntitySystem
{
    [Dependency] private readonly SolutionContainerSystem _solution = default!;

    private const float UpdateInterval = 0.5f;
    private const float HeatedSolutionTemperature = 450f;
    private float _accumulator;

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<SolutionContainerManagerComponent>();
        while (query.MoveNext(out var uid, out var manager))
        {
            var isHotEvent = new IsHotEvent();
            RaiseLocalEvent(uid, isHotEvent, true);
            if (!isHotEvent.IsHot)
                continue;

            foreach (var (_, solutionEnt) in _solution.EnumerateSolutions((uid, manager)))
            {
                if (solutionEnt.Comp.Solution.Temperature >= HeatedSolutionTemperature)
                    continue;

                solutionEnt.Comp.Solution.Temperature = HeatedSolutionTemperature;
                _solution.UpdateChemicals(solutionEnt);
            }
        }
    }
}
