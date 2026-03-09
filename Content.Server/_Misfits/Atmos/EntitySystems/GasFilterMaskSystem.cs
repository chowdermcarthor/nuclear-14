// #Misfits Change
using Content.Server._Misfits.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Atmos.EntitySystems;

/// <summary>
/// Manages gas-filtering clothing items (<see cref="GasFilterMaskComponent"/>).
/// When a filter mask/helmet is equipped in a valid slot, a <see cref="GasFilterActiveComponent"/>
/// is added to the wearer and the mob-level filtered-gas set is kept up to date.
/// During inhalation the system zeroes out filtered gas species in the ambient tile mixture
/// before the lungs receive it, simulating the mask absorbing those gases.
/// </summary>
public sealed class GasFilterMaskSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasFilterMaskComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<GasFilterMaskComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<GasFilterActiveComponent, InhaleLocationEvent>(OnInhaleLocation);
    }

    private void OnEquipped(Entity<GasFilterMaskComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.AllowedSlots) == 0)
            return;

        var active = EnsureComp<GasFilterActiveComponent>(args.Equipee);
        active.ActiveSources[ent.Owner] = ent.Comp.FilteredGases;
        RecalculateFilter(active);
    }

    private void OnUnequipped(Entity<GasFilterMaskComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<GasFilterActiveComponent>(args.Equipee, out var active))
            return;

        active.ActiveSources.Remove(ent.Owner);

        if (active.ActiveSources.Count == 0)
            RemComp<GasFilterActiveComponent>(args.Equipee);
        else
            RecalculateFilter(active);
    }

    private static void RecalculateFilter(GasFilterActiveComponent active)
    {
        active.FilteredGases.Clear();
        foreach (var gases in active.ActiveSources.Values)
            foreach (var gas in gases)
                active.FilteredGases.Add(gas);
    }

    /// <summary>
    /// Intercepts inhalation before the lungs receive ambient gas.
    /// If a tank is already supplying air (args.Gas already set), we skip — clean tank air
    /// does not need filtering.  Otherwise we fetch the ambient mixture, zero out filtered
    /// gas species (the mask absorbs them), and hand the result to the respiratory system.
    /// </summary>
    private void OnInhaleLocation(Entity<GasFilterActiveComponent> ent, ref InhaleLocationEvent args)
    {
        if (args.Gas != null || ent.Comp.FilteredGases.Count == 0)
            return;

        var ambient = _atmosphere.GetContainingMixture(ent.Owner, excite: true);
        if (ambient == null)
            return;

        // Zero out filtered gas moles directly in the tile mixture.
        // This simulates the filter absorbing those gases before the breath is drawn.
        foreach (var gas in ent.Comp.FilteredGases)
            ambient.SetMoles(gas, 0f);

        args.Gas = ambient;
    }
}
