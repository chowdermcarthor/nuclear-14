// #Misfits Change - Ported from Delta-V surgery contamination system
using Content.Client.Items;
using Content.Shared._Misfits.Surgery.Contamination;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Client._Misfits.Surgery.Contamination;

/// <summary>
///     Registers the surgery dirtiness item status bar for held surgical instruments.
///     Shows a colored cleanliness indicator in the HUD.
/// </summary>
public sealed class SurgeryCleanStatusSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<SurgeryDirtinessComponent>(ent =>
            new SurgeryDirtinessItemStatus(ent, EntityManager, _inventory, _container));
    }
}
