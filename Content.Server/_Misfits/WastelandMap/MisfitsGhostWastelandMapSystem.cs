// Grants the Wendover map action to ghosted observers so they can view the
// area map while spectating. The MobObserver entity already carries
// WastelandMapComponent + UserInterfaceComponent (added in observer.yml), so
// the OpenUiActionEvent fired by this action opens the map BUI directly on
// the ghost entity itself.
using Content.Shared.Actions;
using Content.Shared.Ghost;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.WastelandMap;

public sealed class MisfitsGhostWastelandMapSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Subscribe to ComponentStartup instead of MapInitEvent — the event bus
        // only permits one system per (component, event) pair and GhostSystem
        // already owns <GhostComponent, MapInitEvent>. ComponentStartup fires
        // at the same logical moment for this purpose and is unclaimed server-side.
        SubscribeLocalEvent<GhostComponent, ComponentStartup>(OnGhostMapInit);
    }

    private void OnGhostMapInit(EntityUid uid, GhostComponent component, ComponentStartup args)
    {
        // Use a local ref — the spawned action entity is owned by the ghost and
        // will be cleaned up automatically when the ghost entity is deleted.
        EntityUid? mapActionEntity = null;
        _actions.AddAction(uid, ref mapActionEntity, "ActionGhostViewWastelandMap");
    }
}
